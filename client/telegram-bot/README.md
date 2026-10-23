# MKN-MS Telegram Bot

Interim operator client for the MKN-MS HTTP API. See
[`docs/operations/telegram-bot-lab.md`](../../docs/operations/telegram-bot-lab.md)
for the lab note: what this bot is, what it is not, and the one
safety boundary it depends on.

Quick start:

    cp .env.example .env
    # fill MKN_BOT_TOKEN and MKN_ADMIN_MAP
    npm install
    npm run build
    npm start

The bot calls the MKN-MS API on `MKN_API_BASE_URL`. It holds no
state and does no domain logic. Every command is a direct HTTP
call to an existing endpoint.

The API's default port is **5020**, from
`backend/src/MknMs.Api/Properties/launchSettings.json`. If the API
is started with a different port, `MKN_API_BASE_URL` must match.
