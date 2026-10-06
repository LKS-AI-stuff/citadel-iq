#!/usr/bin/env python3
"""
PreToolUse hook (Edit|Write): blocks writing what looks like a real OpenAI API key
into any appsettings*.json file. CLAUDE.md is explicit that OpenAI:ApiKey must only
ever be set via `dotnet user-secrets` / environment variables, never committed to
appsettings.json.
"""
import json
import re
import sys


def main() -> None:
    try:
        payload = json.load(sys.stdin)
    except Exception:
        sys.exit(0)  # can't parse the payload — fail open, don't block

    tool_name = payload.get("tool_name", "")
    tool_input = payload.get("tool_input", {}) or {}
    file_path = tool_input.get("file_path", "") or ""

    if not re.search(r"appsettings(\.[A-Za-z]+)?\.json$", file_path):
        sys.exit(0)

    if tool_name == "Write":
        content = tool_input.get("content", "") or ""
    elif tool_name == "Edit":
        content = tool_input.get("new_string", "") or ""
    else:
        sys.exit(0)

    if re.search(r"sk-[A-Za-z0-9_-]{20,}", content):
        print(
            f"Blocked: this write adds what looks like a real OpenAI API key to {file_path}. "
            'CLAUDE.md requires OpenAI:ApiKey to be set via '
            '`dotnet user-secrets set "OpenAI:ApiKey" "..."` (or an environment variable in '
            "deployed environments) — never committed to appsettings.json.",
            file=sys.stderr,
        )
        sys.exit(2)

    if re.search(r"AccountKey=|SharedAccessSignature=|sig=[A-Za-z0-9%+/=]{20,}", content):
        print(
            f"Blocked: this write adds what looks like an Azure storage key/SAS to {file_path}. "
            'Set Storage:AzureBlob:ConnectionString via `dotnet user-secrets` or the '
            "Storage__AzureBlob__ConnectionString environment variable — never in appsettings.json.",
            file=sys.stderr,
        )
        sys.exit(2)

    sys.exit(0)


if __name__ == "__main__":
    main()
