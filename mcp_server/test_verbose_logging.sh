#!/bin/bash

# 详细日志测试脚本
# 此脚本用于测试MCP服务器的verbose日志功能

echo "=== Unity MCP Server 详细日志测试 ==="
echo "测试时间: $(date)"
echo ""

# 检查可执行文件
if [ ! -f "./unity-mcp-server" ]; then
    echo "❌ 找不到unity-mcp-server可执行文件"
    echo "请先运行: go build -o unity-mcp-server ."
    exit 1
fi

echo "✅ 找到MCP服务器可执行文件"

# 启动debug模式的MCP服务器
echo ""
echo "🚀 启动MCP服务器 (Debug模式)..."
echo "注意: 这将启动双服务器架构:"
echo "  - 端口13000: MCP SSE服务器 (主服务器)"
echo "请确保这些端口没有被占用"
echo ""

# 使用debug模式启动服务器
echo "执行命令: ./unity-mcp-server -debug -port=13000 -unity-host=localhost -unity-port=12000"
echo ""
echo "=== 服务器日志输出 (按Ctrl+C停止) ==="
echo ""

./unity-mcp-server -debug -port=13000 -unity-host=localhost -unity-port=12000