#!/usr/bin/env python3
"""Minimal JSON-RPC client for the Unity MCP server (streamable HTTP)."""

import json
import sys
import urllib.request


MCP_URL = "http://127.0.0.1:8080/mcp"


class UnityMcp:
    def __init__(self, url=MCP_URL):
        self.url = url
        self.session_id = None
        self._initialize()

    def _post(self, payload, timeout=120):
        data = json.dumps(payload).encode("utf-8")
        headers = {
            "Accept": "application/json, text/event-stream",
            "Content-Type": "application/json",
        }
        if self.session_id:
            headers["mcp-session-id"] = self.session_id
        req = urllib.request.Request(self.url, data=data, headers=headers, method="POST")
        with urllib.request.urlopen(req, timeout=timeout) as resp:
            self.session_id = self.session_id or resp.headers.get("mcp-session-id")
            body = resp.read().decode("utf-8")
        return self._parse(body)

    @staticmethod
    def _parse(body):
        if not body.strip():
            return None
        messages = []
        for line in body.splitlines():
            if line.startswith("data: "):
                messages.append(json.loads(line[6:]))
        if not messages:
            raise RuntimeError("No JSON-RPC message in response: " + body[:300])
        return messages[-1]

    def _initialize(self):
        payload = {
            "jsonrpc": "2.0",
            "id": 1,
            "method": "initialize",
            "params": {
                "protocolVersion": "2024-11-05",
                "capabilities": {},
                "clientInfo": {"name": "codex-unity-helper", "version": "1.0"},
            },
        }
        self._post(payload)
        self._post({"jsonrpc": "2.0", "method": "notifications/initialized"})

    def call(self, tool, params=None):
        params = params or {}
        payload = {
            "jsonrpc": "2.0",
            "id": 2,
            "method": "tools/call",
            "params": {"name": tool, "arguments": params},
        }
        msg = self._post(payload)
        if "error" in msg:
            raise RuntimeError("MCP error: " + json.dumps(msg["error"], ensure_ascii=False))
        result = msg.get("result", {})
        if result.get("isError"):
            raise RuntimeError("Tool error: " + json.dumps(result, ensure_ascii=False)[:2000])
        return result.get("content", [])

    def call_json(self, tool, params=None):
        content = self.call(tool, params)
        texts = []
        for item in content:
            if item.get("type") == "text":
                texts.append(item["text"])
            elif item.get("type") == "json":
                texts.append(json.dumps(item.get("json", {}), ensure_ascii=False))
        joined = "\n".join(texts)
        try:
            return json.loads(joined)
        except (ValueError, json.JSONDecodeError):
            return joined

    def list_tools(self):
        payload = {"jsonrpc": "2.0", "id": 3, "method": "tools/list"}
        msg = self._post(payload)
        return msg.get("result", {}).get("tools", [])

    def read_resource(self, uri):
        payload = {
            "jsonrpc": "2.0",
            "id": 4,
            "method": "resources/read",
            "params": {"uri": uri},
        }
        msg = self._post(payload)
        contents = msg.get("result", {}).get("contents", [])
        return [c.get("text", "") for c in contents]


def main():
    if len(sys.argv) < 2:
        print("usage: python unity_mcp.py <tool> [json-params]")
        sys.exit(1)
    tool = sys.argv[1]
    if tool == "tools/list":
        client = UnityMcp()
        tools = client.list_tools()
        print(json.dumps([{"name": t.get("name")} for t in tools], ensure_ascii=False, indent=2))
        sys.exit(0)
    if tool == "resource":
        client = UnityMcp()
        uri = sys.argv[2]
        for text in client.read_resource(uri):
            print(text)
        sys.exit(0)
    params = json.loads(sys.argv[2]) if len(sys.argv) > 2 else {}
    client = UnityMcp()
    result = client.call_json(tool, params)
    print(json.dumps(result, ensure_ascii=False, indent=2) if not isinstance(result, str) else result)


if __name__ == "__main__":
    main()
