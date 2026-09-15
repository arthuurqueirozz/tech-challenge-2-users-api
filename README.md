# FIAP Cloud Games - UsersAPI

## Métricas da Fase 3

`GET /metrics` expõe Prometheus (prometheus-net.AspNetCore 8.2.1).
`fcg_http_requests_total` conta requisições de negócio; o histograma
`fcg_http_request_duration_seconds` mede sua duração. Labels: `route` com template
do endpoint (ou `unmatched`), `method` com verbos conhecidos/`OTHER` e `status_code`.
Não há labels com usuários, tokens, query strings ou IDs concretos. Apenas `/api`
é instrumentado; health e scraping são excluídos. Middleware envolve o tratamento
de exceções para registrar o status final, inclusive 400/401/500.

Prometheus/Grafana e o dashboard provisionado ficam no repositório de
[orquestração, etapa 6](https://github.com/arthuurqueirozz/tech-challenge-3-orchestration/blob/main/docs/ETAPA-6.md).
O scraper acrescenta `job=users-api`. `/metrics` é interno ao cluster e não deve
receber rota pública no gateway. Contadores reiniciam com o processo; taxas usam
`rate`, que trata resets. Gate: 29 testes UsersAPI e 33 verificações integradas
da stack de métricas passaram.

Microsservico de usuarios evoluido para a Fase 3 do Tech Challenge FIAP. Ele e
responsavel por:

- cadastrar usuarios;
- autenticar credenciais e emitir tokens JWT;
- autorizar acesso ao perfil autenticado;
- registrar `UserCreatedEvent` em outbox transacional e publicar JSON direto no SQS.

A Lambda Notifications consome esse evento e registra a simulacao de boas-vindas
no DynamoDB. A baseline da Fase 2 foi preservada na tag `fase-2-final`.

## Tecnologias

- .NET 8
- ASP.NET Core Minimal APIs
- Entity Framework Core 8
- SQL Server
- AWS SDK SQS 4.0.100.13
- JWT Bearer
- Serilog
- xUnit
- Docker
- Kubernetes

## Estrutura

```text
.
├── src/
│   ├── FCG.Users.Api/
│   ├── FCG.Users.Domain/
│   └── FCG.Users.Infrastructure/
├── tests/FCG.Users.Tests/
├── k8s/
├── Dockerfile
└── TechChallenge.Users.sln
```

O banco deste microsservico contem a tabela `Users` e a tabela tecnica
`IntegrationOutboxMessages`, usada para garantir a publicacao de eventos apos
falhas temporarias do SQS. As migrations ficam em
`FCG.Users.Infrastructure/Persistence/Migrations`.

## Endpoints

| Metodo | Rota | Autenticacao | Finalidade |
|---|---|---|---|
| `POST` | `/api/auth/register` | Anonima | Cadastra usuario, emite JWT e registra o evento para publicacao confiavel. |
| `POST` | `/api/auth/login` | Anonima | Valida credenciais e emite JWT. |
| `GET` | `/api/me/profile` | Bearer JWT | Retorna o perfil do usuario autenticado. |
| `GET` | `/health` | Anonima | Readiness com SQL Server; cadastro continua disponivel se SQS falhar, pois o outbox e duravel. |
| `GET` | `/health/live` | Anonima | Liveness do processo. |
| `GET` | `/swagger/index.html` | Anonima | Interface Swagger em `Development`. |

Exemplo de cadastro:

```json
{
  "name": "Ada Lovelace",
  "email": "ada@example.com",
  "password": "ValidPass1!"
}
```

A senha deve ter pelo menos oito caracteres, incluindo letra, numero e
caractere especial. E-mails sao normalizados para minusculas e possuem indice
unico no banco.

## Contrato de integracao

O contrato usa o namespace `FCG.IntegrationEvents.V1`.

`UserCreatedEvent`:

- `EventId`
- `OccurredAtUtc`
- `UserId`
- `Name`
- `Email`

O evento e gravado no outbox na mesma persistencia do usuario. Um servico em
background publica o evento no SQS e tenta novamente se houver falha.
O EventId e o payload persistidos sao preservados; ProcessedAtUtc so e marcado
apos SendMessage concluir. O backoff existente e mantido. Um envio aceito cuja
resposta se perca pode ser repetido; a Lambda trata duplicidade por EventId.
RabbitMQ/MassTransit foram removidos deste servico porque nao havia outro uso.
O worker registra as falhas; a saude SQL nao comprova entrega das notificacoes.

## Variaveis de ambiente

| Variavel | Sensivel | Descricao |
|---|---|---|
| `ConnectionStrings__UsersDatabase` | Sim | Connection string exclusiva do SQL Server da UsersAPI. |
| `Jwt__Issuer` | Nao | Emissor esperado no JWT. |
| `Jwt__Audience` | Nao | Audiencia esperada no JWT. |
| `Jwt__Key` | Sim | Chave simetrica com no minimo 32 bytes. |
| `Jwt__AccessTokenMinutes` | Nao | Validade do token em minutos. |
| `Sqs__Region` | Nao | Regiao AWS; us-east-1. |
| `Sqs__QueueUrl` | Nao | URL HTTPS da fila de cadastro na mesma regiao. |
| `AWS_ACCESS_KEY_ID` | Sim | Credencial temporaria da role users publisher. |
| `AWS_SECRET_ACCESS_KEY` | Sim | Segredo temporario. |
| `AWS_SESSION_TOKEN` | Sim | Token da sessao STS; renovar antes de expirar. |
| `AdminSeed__Enabled` | Nao | Habilita a criacao idempotente do administrador inicial. |
| `AdminSeed__Name` | Nao | Nome do administrador inicial. |
| `AdminSeed__Email` | Nao | E-mail do administrador inicial. |
| `AdminSeed__Password` | Sim | Senha forte do administrador inicial. |

Credenciais, connection strings e a chave JWT nao ficam no
`appsettings.json`.

## Executar localmente

Inicie SQL Server substituindo os placeholders por valores locais:

```bash
docker run --name tc2-users-sql \
  -e ACCEPT_EULA=Y \
  -e MSSQL_SA_PASSWORD='<sql-password>' \
  -p 1433:1433 \
  -d mcr.microsoft.com/mssql/server:2022-latest

```

Configure a aplicacao:

```bash
export ConnectionStrings__UsersDatabase='Server=localhost,1433;Database=FcgUsersDb;User Id=sa;Password=<sql-password>;TrustServerCertificate=True;Encrypt=False'
export Jwt__Key='<jwt-key-with-at-least-32-bytes>'
export Sqs__Region=us-east-1
export Sqs__QueueUrl='https://sqs.us-east-1.amazonaws.com/ACCOUNT_ID/fcg-fase3-user-created'
# Injete AWS_ACCESS_KEY_ID, AWS_SECRET_ACCESS_KEY e AWS_SESSION_TOKEN da role
# de publicacao via mecanismo local. Nunca use a credencial de deploy no container.
```

Restaure e execute:

```bash
dotnet restore
dotnet run --project src/FCG.Users.Api/FCG.Users.Api.csproj
```

A migration e aplicada automaticamente na inicializacao. Em
`Development`, abra `/swagger/index.html`, execute cadastro ou login, clique em
`Authorize`, informe apenas o token JWT sem o prefixo `Bearer` e chame
`/api/me/profile`. Em chamadas manuais com `curl` ou outro cliente HTTP, use o
cabecalho `Authorization: Bearer {token}`.

O administrador inicial e opcional. Para cria-lo:

```bash
export AdminSeed__Enabled=true
export AdminSeed__Password='<admin-password>'
```

## Testes

```bash
dotnet test tests/FCG.Users.Tests/FCG.Users.Tests.csproj
```

Tambem e possivel usar o runner nativo do xUnit v3:

```bash
dotnet run --project tests/FCG.Users.Tests/FCG.Users.Tests.csproj
```

Os testes cobrem regras de dominio, validadores, hash de senha, claims JWT,
cadastro, login, enfileiramento transacional do evento, outbox, repositorio e
restricao de e-mail unico.
Incluem falha de envio SQS, persistencia do retry/backoff e recuperacao usando
o mesmo EventId. O teste do outbox usa SQLite real e um cliente SQS simulado.

## Docker

Na raiz do repositorio:

```bash
docker build -t tech-challenge-2-users-api:local .
```

O Dockerfile usa build multi-stage e executa a imagem final com ASP.NET Core
Runtime 8 e usuario nao root.

## Kubernetes

Os manifests obrigatorios ficam em `/k8s`:

- `deployment.yaml`;
- `service.yaml`;
- `configmap.yaml`;
- `secret.example.yaml`.

Crie o arquivo local de Secret:

```bash
cp k8s/secret.example.yaml k8s/secret.local.yaml
```

Substitua todos os placeholders e aplique:

```bash
kubectl apply -f k8s/configmap.yaml
kubectl apply -f k8s/secret.local.yaml
kubectl apply -f k8s/deployment.yaml
kubectl apply -f k8s/service.yaml
```

O arquivo `secret.local.yaml` e ignorado pelo Git. Dentro do cluster, a API e
acessada pelos demais componentes em `http://users-api:80`.

O ensaio dos produtores da Fase 3 esta em `compose.stage4.yaml` no repositorio
[Orchestration](https://github.com/arthuurqueirozz/tech-challenge-3-orchestration).
Ele prepara credenciais temporarias e configura as tres APIs com SQL/RabbitMQ.
A stack Kind/Kong final pertence a orquestracao e sera validada nas etapas seguintes.
