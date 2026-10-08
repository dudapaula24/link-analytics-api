# Link Analytics API

[English](README.md) | **Português (Brasil)**

API REST em ASP.NET Core que encurta URLs, redireciona visitantes para o endereço original e informa quantas vezes cada link curto foi acessado.

> Todos os dados deste repositório são fictícios. URLs como `https://example.com/docs` e domínios como `links.example.com` são endereços reservados para exemplos.

## Visão geral

Links curtos são mais fáceis de compartilhar do que URLs longas, e contar seus acessos mostra quais links são realmente usados. Um encurtador precisa fazer bem três coisas: gerar códigos únicos e difíceis de adivinhar, redirecionar rapidamente e registrar cada acesso sem coletar mais dados do que o necessário.

Este projeto implementa esse núcleo. Ele valida e armazena URLs, gera códigos curtos aleatórios, redireciona `/r/{code}` para o endereço original registrando o acesso e retorna estatísticas por link: total de acessos, último acesso e acessos por dia. Nenhum endereço IP, localização ou outro dado do visitante é armazenado.

## Início rápido

```bash
git clone https://github.com/dudapaula24/link-analytics-api.git
cd link-analytics-api
dotnet tool restore
dotnet run --project src/LinkAnalytics.Api
```

A API responde em `http://localhost:5087`. No ambiente de Development o banco SQLite é criado automaticamente, sem nenhuma configuração extra.

```bash
curl http://localhost:5087/health
```

## Funcionalidades

- **Validação de URL**: apenas URLs absolutas `http`/`https` de até 2048 caracteres, sem credenciais embutidas.
- **Códigos curtos aleatórios**: 7 caracteres Base62 gerados por um gerador criptograficamente seguro, não sequenciais e difíceis de adivinhar.
- **Unicidade garantida**: índice único no banco e nova tentativa automática quando um código gerado já existe.
- **Redirecionamento temporário**: `302 Found` com `Cache-Control: no-store`, para que todo acesso chegue à API e seja contado.
- **Estatísticas de acesso**: total de acessos, último acesso e acessos por dia, calculados no banco de dados.
- **Privacidade desde o projeto**: apenas a data e a hora de cada acesso são armazenadas.
- **Links curtos seguros**: montados a partir de uma URL base pública configurada, nunca do cabeçalho `Host` da requisição.
- **Erros padronizados**: todas as respostas de erro seguem o formato [RFC 9457 Problem Details](https://www.rfc-editor.org/rfc/rfc9457).
- **Documento OpenAPI** disponível em Development.

## Tecnologias

| Ferramenta | Uso |
|---|---|
| .NET 10 (LTS) / ASP.NET Core Minimal APIs | Linguagem, runtime e endpoints HTTP |
| Entity Framework Core 10 | Acesso a dados e migrations |
| SQLite | Banco de dados |
| Microsoft.AspNetCore.OpenApi | Geração do documento OpenAPI 3.1 |
| xUnit + `WebApplicationFactory` | Testes unitários e de integração |

- **Requisitos:** .NET SDK 10.0.301 ou superior (fixado no `global.json`).
- **Testado com:** .NET SDK 10.0.401, ASP.NET Core 10.0.12, Entity Framework Core 10.0.12 e xUnit 2.9.3.

## Como funciona

```
POST /api/links ────────────► valida URL ─► gera código único ─► salva ShortLink ─► 201 Created

GET  /r/{code} ─────────────► busca link ─► salva LinkClick (UTC) ─► 302 Found

GET  /api/links/{code}/stats ► busca link ─► COUNT / GROUP BY dia / MAX no SQL ─► 200 OK
```

### Códigos curtos

Cada código tem 7 caracteres do alfabeto Base62 (`0-9`, `A-Z`, `a-z`), o que dá cerca de 3,5 trilhões de combinações. Os códigos diferenciam maiúsculas de minúsculas. Antes de salvar, a API verifica se o código já existe e gera outro se necessário. Se duas requisições ainda assim inserirem o mesmo código no mesmo instante, o índice único rejeita a segunda e a API tenta novamente, até 5 vezes.

### Estatísticas de acesso

Cada requisição para `/r/{code}` grava um acesso com seu horário em UTC e então redireciona. O endpoint de estatísticas conta os acessos, agrupa por dia em UTC e encontra o mais recente usando agregações SQL (`COUNT`, `GROUP BY`, `MAX`), apoiadas por um índice em `(ShortLinkId, ClickedAt)`. Os registros individuais de acesso nunca são carregados em memória.

### Decisões técnicas

- **`302` em vez de `301`.** Navegadores guardam redirecionamentos permanentes em cache e deixam de chamar a API, então acessos seriam perdidos. O `Cache-Control: no-store` também impede que proxies armazenem a resposta.
- **Banco de dados como garantia final de unicidade.** Verificar antes de inserir evita a maioria das colisões, mas só o índice único é seguro contra requisições simultâneas.
- **URL base pública configurada.** O cabeçalho `Host` é controlado pelo cliente e pode ser falsificado, por isso os links curtos são montados a partir da configuração. A API não inicia se o valor estiver ausente ou inválido.
- **UTC em todo lugar.** As datas são armazenadas e retornadas em UTC, então os resultados não dependem do fuso horário do servidor.
- **Bancos de teste isolados.** Cada teste de integração usa seu próprio banco SQLite em memória, criado com as migrations reais, então os testes nunca compartilham dados e também validam o schema.

## Configuração

| Configuração | Variável de ambiente | Descrição | Padrão |
|---|---|---|---|
| `ShortLinks:PublicBaseUrl` | `ShortLinks__PublicBaseUrl` | Endereço público usado para montar os links curtos. URL HTTP/HTTPS absoluta, pode incluir um caminho (`https://example.com/go`), sem query string, fragmento ou credenciais. **Obrigatória.** | `http://localhost:5087` em Development, vazio nos demais |
| `AllowedHosts` | `AllowedHosts` | Nomes de host aceitos pela API, separados por ponto e vírgula. Outros cabeçalhos `Host` recebem `400`. | `localhost;127.0.0.1;[::1]` |
| `ConnectionStrings:LinkAnalytics` | `ConnectionStrings__LinkAnalytics` | Connection string do SQLite. | `Data Source=linkanalytics.db` |

Fora de Development, configure pelo menos a URL base pública e os hosts permitidos:

```bash
export ASPNETCORE_ENVIRONMENT=Production
export ShortLinks__PublicBaseUrl=https://links.example.com
export AllowedHosts=links.example.com
```

Quando há um endpoint HTTPS configurado, requisições HTTP são redirecionadas para HTTPS. O HSTS é ativado fora de Development. Para rodar localmente com HTTPS (`https://localhost:7129`):

```bash
dotnet dev-certs https --trust
dotnet run --project src/LinkAnalytics.Api --launch-profile https
```

## Banco de dados e migrations

O arquivo `linkanalytics.db` é criado no diretório de trabalho, que é `src/LinkAnalytics.Api/` ao usar `dotnet run --project` ou `dotnet ef`. Arquivos de banco de dados são ignorados pelo Git.

- **Development:** migrations pendentes são aplicadas automaticamente na inicialização.
- **Demais ambientes:** aplique as migrations antes de iniciar a API:

```bash
dotnet ef database update --project src/LinkAnalytics.Api
```

Para criar uma nova migration após alterar o modelo:

```bash
dotnet ef migrations add <NomeDaMigration> --project src/LinkAnalytics.Api --output-dir Data/Migrations
```

| Tabela | Colunas |
|---|---|
| `ShortLinks` | `Id`, `OriginalUrl` (máx. 2048), `Code` (único), `CreatedAt` (UTC) |
| `LinkClicks` | `Id`, `ShortLinkId` (chave estrangeira, exclusão em cascata), `ClickedAt` (UTC) |

## Endpoints da API

| Método | Rota | Descrição | Respostas |
|---|---|---|---|
| `GET` | `/health` | Status de saúde da API | `200` |
| `POST` | `/api/links` | Cria um link curto | `201`, `400` |
| `GET` | `/api/links/{code}` | Retorna os detalhes de um link curto | `200`, `404` |
| `GET` | `/api/links/{code}/stats` | Retorna as estatísticas de acesso de um link curto | `200`, `404` |
| `GET` | `/r/{code}` | Registra um acesso e redireciona para a URL original | `302`, `404` |

As respostas de erro usam o formato `application/problem+json`. Em Development, o documento OpenAPI fica disponível em `http://localhost:5087/openapi/v1.json`.

### Regras de URL

- Obrigatória, absoluta, com `http` ou `https`.
- Credenciais na URL (`https://user:pass@host`) são rejeitadas.
- Tamanho máximo de 2048 caracteres.
- Normalizada antes de ser armazenada: `HTTP://Example.COM` vira `http://example.com/`.
- Cada requisição cria um novo link curto, mesmo para uma URL que já foi encurtada.

## Exemplos de requisição

Requisições prontas para todos os endpoints também estão em [`src/LinkAnalytics.Api/LinkAnalytics.Api.http`](src/LinkAnalytics.Api/LinkAnalytics.Api.http).

**Criar um link curto**

```bash
curl -i -X POST http://localhost:5087/api/links \
  -H "Content-Type: application/json" \
  -d '{"url": "https://example.com/docs?page=1"}'
```

```http
HTTP/1.1 201 Created
Location: /api/links/aZ3kP9x
```

```json
{
  "id": 1,
  "code": "aZ3kP9x",
  "originalUrl": "https://example.com/docs?page=1",
  "shortUrl": "http://localhost:5087/r/aZ3kP9x",
  "createdAt": "2026-10-08T12:00:00Z"
}
```

**URL inválida**

```bash
curl -X POST http://localhost:5087/api/links \
  -H "Content-Type: application/json" \
  -d '{"url": "ftp://example.com/file.txt"}'
```

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
  "title": "One or more validation errors occurred.",
  "status": 400,
  "errors": {
    "url": ["The URL must be an absolute HTTP or HTTPS address."]
  }
}
```

**Consultar um link curto**

```bash
curl http://localhost:5087/api/links/aZ3kP9x
```

Retorna `200 OK` com o mesmo corpo da criação, ou `404 Not Found`:

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.5",
  "title": "Not Found",
  "status": 404
}
```

**Redirecionamento**

```bash
curl -i http://localhost:5087/r/aZ3kP9x
```

```http
HTTP/1.1 302 Found
Cache-Control: no-store
Location: https://example.com/docs?page=1
```

Um código inexistente retorna `404 Not Found` e nada é registrado.

**Estatísticas de acesso**

```bash
curl http://localhost:5087/api/links/aZ3kP9x/stats
```

```json
{
  "code": "aZ3kP9x",
  "totalClicks": 5,
  "lastClickAt": "2026-10-04T08:00:00Z",
  "clicksByDay": [
    { "date": "2026-10-01", "clicks": 3 },
    { "date": "2026-10-02", "clicks": 1 },
    { "date": "2026-10-04", "clicks": 1 }
  ]
}
```

Os dias são em UTC e ordenados cronologicamente; dias sem acesso são omitidos. Um link sem acessos retorna `"totalClicks": 0`, `"lastClickAt": null` e `"clicksByDay": []`.

## Executando os testes

```bash
dotnet test
```

Os testes cobrem:

- **Criação de links:** URLs válidas e inválidas, normalização, credenciais, tamanho máximo, corpo malformado ou vazio, códigos únicos e nova tentativa após colisão de código.
- **Consulta e redirecionamento:** códigos existentes e inexistentes, diferenciação de maiúsculas e minúsculas, respostas `302` e `Cache-Control: no-store`.
- **Estatísticas:** registro de acessos, múltiplos acessos, agrupamento por dia em UTC, links sem acessos, códigos inexistentes e isolamento entre links.
- **Configuração:** links curtos montados a partir da URL base pública, falha na inicialização com valores inválidos, cabeçalhos `Host` rejeitados e redirecionamento para HTTPS.
- **Gerador de códigos e health check.**

Os testes de integração hospedam a API em memória com `WebApplicationFactory`. Cada teste recebe seu próprio banco SQLite em memória, e um relógio controlável (`TimeProvider`) torna determinísticos os testes que dependem de datas.

## Estrutura do projeto

```text
link-analytics-api/
├── src/
│   └── LinkAnalytics.Api/
│       ├── Contracts/                # modelos de requisição e resposta
│       ├── Data/                     # DbContext e migrations do EF Core
│       ├── Endpoints/                # definição dos endpoints por funcionalidade
│       ├── Models/                   # entidades (ShortLink, LinkClick)
│       ├── Services/                 # criação de links, estatísticas, geração de códigos, validação
│       ├── appsettings*.json         # configuração
│       ├── LinkAnalytics.Api.http    # exemplos de requisição
│       └── Program.cs                # registro de serviços e pipeline HTTP
├── tests/
│   └── LinkAnalytics.Tests/
│       ├── Endpoints/                # testes de integração
│       ├── Infrastructure/           # host de teste com banco isolado
│       └── Services/                 # testes unitários
├── dotnet-tools.json                 # ferramentas locais (dotnet-ef)
├── global.json                       # versão do .NET SDK
└── LinkAnalytics.slnx                # arquivo da solução
```

## Limitações

- Todo `GET` em `/r/{code}` conta como acesso, inclusive robôs, buscadores e pré-visualizações de links em aplicativos de mensagem. Filtrá-los exigiria analisar dados do visitante, que a API intencionalmente não coleta.
- As estatísticas são agrupadas por dia em UTC: um acesso às 22h em UTC−3 conta para o dia seguinte.
- O acesso é registrado antes do redirecionamento. Se a gravação no banco falhar, o visitante recebe um erro `500` em vez de ser redirecionado.
- Não há autenticação nem limite de requisições. Qualquer pessoa com acesso à API pode criar links e ler estatísticas, então ela não deve ser exposta publicamente como está.
- Links não podem ser editados, excluídos nem ter data de expiração.
- O SQLite atende a uma única instância; várias instâncias exigiriam um banco de dados servidor.
- Os forwarded headers não estão configurados. Atrás de um proxy ou balanceador que encerre o TLS, o redirecionamento para HTTPS precisaria de configuração adicional.

## Melhorias futuras

- Autenticação e limite de requisições.
- Expiração e exclusão de links.
- Estatísticas no fuso horário escolhido pelo cliente.
- Um banco de dados servidor, como PostgreSQL, para várias instâncias.
- Imagem Docker.

## Autoria

Desenvolvido por [dudapaula24](https://github.com/dudapaula24).
