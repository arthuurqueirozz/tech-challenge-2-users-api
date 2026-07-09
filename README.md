# FIAP Cloud Games - UsersAPI

Microsservico de usuarios da Fase 2 do Tech Challenge FIAP. Ele e
responsavel por:

- cadastrar usuarios;
- autenticar credenciais e emitir tokens JWT;
- autorizar acesso ao perfil autenticado;
- registrar `UserCreatedEvent` em outbox transacional e publicar o evento no RabbitMQ.

O `NotificationsAPI` consome esse evento e simula o envio do e-mail de
boas-vindas.

## Tecnologias

- .NET 8
- ASP.NET Core Minimal APIs
- Entity Framework Core 8
- SQL Server
- MassTransit 8.5.10
- RabbitMQ
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
falhas temporarias do RabbitMQ. As migrations ficam em
`FCG.Users.Infrastructure/Persistence/Migrations`.

## Endpoints

| Metodo | Rota | Autenticacao | Finalidade |
|---|---|---|---|
| `POST` | `/api/auth/register` | Anonima | Cadastra usuario, emite JWT e registra o evento para publicacao confiavel. |
| `POST` | `/api/auth/login` | Anonima | Valida credenciais e emite JWT. |
| `GET` | `/api/me/profile` | Bearer JWT | Retorna o perfil do usuario autenticado. |
| `GET` | `/health` | Anonima | Readiness, incluindo conexao com SQL Server e RabbitMQ. |
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
background publica o evento no RabbitMQ e tenta novamente se houver falha
temporaria no broker.

## Variaveis de ambiente

| Variavel | Sensivel | Descricao |
|---|---|---|
| `ConnectionStrings__UsersDatabase` | Sim | Connection string exclusiva do SQL Server da UsersAPI. |
| `Jwt__Issuer` | Nao | Emissor esperado no JWT. |
| `Jwt__Audience` | Nao | Audiencia esperada no JWT. |
| `Jwt__Key` | Sim | Chave simetrica com no minimo 32 bytes. |
| `Jwt__AccessTokenMinutes` | Nao | Validade do token em minutos. |
| `RabbitMq__Host` | Nao | Nome DNS ou host do RabbitMQ. |
| `RabbitMq__Port` | Nao | Porta AMQP, normalmente `5672`. |
| `RabbitMq__VirtualHost` | Nao | Virtual host do broker. |
| `RabbitMq__Username` | Sim | Usuario do RabbitMQ. |
| `RabbitMq__Password` | Sim | Senha do RabbitMQ. |
| `AdminSeed__Enabled` | Nao | Habilita a criacao idempotente do administrador inicial. |
| `AdminSeed__Name` | Nao | Nome do administrador inicial. |
| `AdminSeed__Email` | Nao | E-mail do administrador inicial. |
| `AdminSeed__Password` | Sim | Senha forte do administrador inicial. |

Credenciais, connection strings e a chave JWT nao ficam no
`appsettings.json`.

## Executar localmente

Inicie SQL Server e RabbitMQ. Os valores abaixo sao apenas para
desenvolvimento local:

```bash
docker run --name tc2-users-sql \
  -e ACCEPT_EULA=Y \
  -e MSSQL_SA_PASSWORD='LocalPass1!' \
  -p 1433:1433 \
  -d mcr.microsoft.com/mssql/server:2022-latest

docker run --name tc2-rabbitmq \
  -e RABBITMQ_DEFAULT_USER=tc2 \
  -e RABBITMQ_DEFAULT_PASS='local-password' \
  -p 5672:5672 \
  -p 15672:15672 \
  -d rabbitmq:4-management
```

Configure a aplicacao:

```bash
export ConnectionStrings__UsersDatabase='Server=localhost,1433;Database=FcgUsersDb;User Id=sa;Password=LocalPass1!;TrustServerCertificate=True;Encrypt=False'
export Jwt__Key='local-development-key-with-at-least-32-bytes'
export RabbitMq__Username='tc2'
export RabbitMq__Password='local-password'
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
export AdminSeed__Password='AdminPass1!'
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

O `docker-compose.yml` que executara os quatro microsservicos juntos sera
mantido no repositorio da CatalogAPI, conforme o planejamento da solucao sem
um quinto repositorio opcional.
