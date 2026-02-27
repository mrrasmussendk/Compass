#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
ENV_FILE="$ROOT_DIR/.env.compass"

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

include_openai_samples="false"
if [[ "$provider" == "openai" ]]; then
  echo
  read -r -p "Include OpenAI samples? (y/N): " include_samples
  if [[ "$include_samples" =~ ^[Yy]$ ]]; then
    include_openai_samples="true"
  fi
fi

echo
echo "Select deployment mode:"
echo "  1) Local console"
echo "  2) Discord channel"
read -r -p "> " deploy_choice

{
  echo "export COMPASS_MODEL_PROVIDER=${provider}"
  echo "export ${key_name}=${api_key}"
  echo "export COMPASS_MODEL_NAME=${selected_model}"
} > "$ENV_FILE"

if [[ "$deploy_choice" == "2" ]]; then
  read -r -p "Enter DISCORD_BOT_TOKEN: " discord_token
  read -r -p "Enter DISCORD_CHANNEL_ID: " discord_channel
  {
    echo "export DISCORD_BOT_TOKEN=${discord_token}"
    echo "export DISCORD_CHANNEL_ID=${discord_channel}"
  } >> "$ENV_FILE"
fi

if [[ "$include_openai_samples" == "true" ]]; then
  echo "export COMPASS_INCLUDE_OPENAI_SAMPLES=true" >> "$ENV_FILE"
fi

cat <<EOF

Configuration saved to: $ENV_FILE

Next steps:
EOF

if [[ -f "$ROOT_DIR/UtilityAi.Compass.sln" && -d "$ROOT_DIR/samples/Compass.SampleHost" ]]; then
  cat <<EOF
  1. dotnet build "$ROOT_DIR/UtilityAi.Compass.sln"
  2. dotnet run --project "$ROOT_DIR/samples/Compass.SampleHost"
EOF
else
  cat <<EOF
  1. Run: compass
  2. Use /help to view available commands.
EOF
fi

cat <<EOF

The host loads .env.compass automatically — no need to source the file.
If Discord variables are configured, the host will start in Discord mode automatically.
EOF

if [[ "$include_openai_samples" == "true" ]]; then
  if [[ -f "$ROOT_DIR/UtilityAi.Compass.sln" && -d "$ROOT_DIR/samples/Compass.SamplePlugin.OpenAi" && -d "$ROOT_DIR/samples/Compass.SampleHost" ]]; then
    cat <<EOF
OpenAI samples enabled. Deploy the plugin before running the host:
  dotnet publish "$ROOT_DIR/samples/Compass.SamplePlugin.OpenAi" -c Release
  mkdir -p "$ROOT_DIR/samples/Compass.SampleHost/bin/Debug/net10.0/plugins"
  cp "$ROOT_DIR/samples/Compass.SamplePlugin.OpenAi/bin/Release/net10.0/publish/"* \\
     "$ROOT_DIR/samples/Compass.SampleHost/bin/Debug/net10.0/plugins/"
EOF
  else
    echo "OpenAI samples enabled. Source repository samples are required to deploy the example plugin."
  fi
fi
