#!/bin/bash

# 测试Unity MCP服务器
echo "=== Unity MCP Server Test ==="

# 检查是否编译成功
if [ ! -f "./unity-mcp-server" ]; then
    echo "❌ 二进制文件不存在，开始编译..."
    go build -o unity-mcp-server . || exit 1
    echo "✅ 编译成功"
fi

echo "🚀 启动MCP服务器 (debug模式)..."
echo "按 Ctrl+C 停止服务器"
echo ""

# 启动服务器，开启debug模式
exec ./unity-mcp-server -debug -port=13000 -unity-host=localhost -unity-port=12000