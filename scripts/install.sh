#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
ENV_FILE="$ROOT_DIR/.env.nexus"

echo "Compass installer"
echo "Select model provider:"
echo "  1) OpenAI"
echo "  2) Anthropic"
echo "  3) Gemini"
read -r -p "> " provider_choice

case "$provider_choice" in
  1) provider="openai"; key_name="OPENAI_API_KEY"; default_model="gpt-4o-mini" ;;
  2) provider="anthropic"; key_name="ANTHROPIC_API_KEY"; default_model="claude-3-5-haiku-latest" ;;
  3) provider="gemini"; key_name="GEMINI_API_KEY"; default_model="gemini-2.0-flash" ;;
  *) echo "Invalid provider choice"; exit 1 ;;
esac

read -r -p "Enter ${key_name}: " api_key
read -r -p "Enter model name [${default_model}]: " selected_model
selected_model="${selected_model:-$default_model}"

echo
echo "Select deployment mode:"
echo "  1) Local console"
echo "  2) Discord channel"
read -r -p "> " deploy_choice

{
  echo "export NEXUS_MODEL_PROVIDER=${provider}"
  echo "export ${key_name}=${api_key}"
  echo "export NEXUS_MODEL_NAME=${selected_model}"
} > "$ENV_FILE"

if [[ "$deploy_choice" == "2" ]]; then
  read -r -p "Enter DISCORD_BOT_TOKEN: " discord_token
  read -r -p "Enter DISCORD_CHANNEL_ID: " discord_channel
  {
    echo "export DISCORD_BOT_TOKEN=${discord_token}"
    echo "export DISCORD_CHANNEL_ID=${discord_channel}"
  } >> "$ENV_FILE"
fi

cat <<EOF

Configuration saved to: $ENV_FILE

Next steps:
  1. source "$ENV_FILE"
  2. dotnet build "$ROOT_DIR/UtilityAi.Nexus.sln"
  3. dotnet run --project "$ROOT_DIR/samples/Nexus.SampleHost"

If Discord variables are configured, the host will start in Discord mode automatically.
EOF
