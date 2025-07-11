# Unity MCP Server Makefile
# 编译多平台Go二进制文件

# 变量定义
BINARY_NAME=unity-mcp-server
VERSION=1.0.0
BIN_DIR=../bin
BUILD_FLAGS=-ldflags "-s -w -X main.version=$(VERSION)"

# 默认目标
.PHONY: all
all: clean deps build

# 安装依赖
.PHONY: deps
deps:
	@echo "📦 安装Go依赖..."
	go mod download
	go mod tidy

# 编译所有平台
.PHONY: build
build: build-windows build-darwin build-linux
	@echo "✅ 所有平台编译完成！"
	@echo ""
	@echo "📁 编译结果："
	@ls -la $(BIN_DIR)/*/

# Windows 64位
.PHONY: build-windows
build-windows:
	@echo "🪟 编译 Windows x64..."
	@mkdir -p $(BIN_DIR)/windows
	GOOS=windows GOARCH=amd64 go build $(BUILD_FLAGS) -o $(BIN_DIR)/windows/$(BINARY_NAME).exe .

# macOS 64位 (Intel)
.PHONY: build-darwin-amd64
build-darwin-amd64:
	@echo "🍎 编译 macOS x64 (Intel)..."
	@mkdir -p $(BIN_DIR)/darwin
	GOOS=darwin GOARCH=amd64 go build $(BUILD_FLAGS) -o $(BIN_DIR)/darwin/$(BINARY_NAME)-amd64 .

# macOS ARM64 (Apple Silicon)
.PHONY: build-darwin-arm64
build-darwin-arm64:
	@echo "🍎 编译 macOS ARM64 (Apple Silicon)..."
	@mkdir -p $(BIN_DIR)/darwin
	GOOS=darwin GOARCH=arm64 go build $(BUILD_FLAGS) -o $(BIN_DIR)/darwin/$(BINARY_NAME)-arm64 .

# macOS 通用二进制 (合并Intel和ARM64)
.PHONY: build-darwin
build-darwin: build-darwin-amd64 build-darwin-arm64
	@echo "🍎 创建 macOS 通用二进制..."
	lipo -create -output $(BIN_DIR)/darwin/$(BINARY_NAME) \
		$(BIN_DIR)/darwin/$(BINARY_NAME)-amd64 \
		$(BIN_DIR)/darwin/$(BINARY_NAME)-arm64
	@rm $(BIN_DIR)/darwin/$(BINARY_NAME)-amd64 $(BIN_DIR)/darwin/$(BINARY_NAME)-arm64
	@chmod +x $(BIN_DIR)/darwin/$(BINARY_NAME)

# Linux 64位
.PHONY: build-linux
build-linux:
	@echo "🐧 编译 Linux x64..."
	@mkdir -p $(BIN_DIR)/linux
	GOOS=linux GOARCH=amd64 go build $(BUILD_FLAGS) -o $(BIN_DIR)/linux/$(BINARY_NAME) .
	@chmod +x $(BIN_DIR)/linux/$(BINARY_NAME)

# 本地运行开发版本
.PHONY: run
run:
	@echo "🚀 启动开发服务器..."
	go run . -port=8080 -unity-host=localhost -unity-port=12345

# 测试
.PHONY: test
test:
	@echo "🧪 运行测试..."
	go test -v ./...

# 清理编译文件
.PHONY: clean
clean:
	@echo "🧹 清理编译文件..."
	@rm -rf $(BIN_DIR)/windows $(BIN_DIR)/darwin $(BIN_DIR)/linux
	@mkdir -p $(BIN_DIR)/windows $(BIN_DIR)/darwin $(BIN_DIR)/linux

# 格式化代码
.PHONY: fmt
fmt:
	@echo "🎨 格式化代码..."
	go fmt ./...

# 检查代码
.PHONY: lint
lint:
	@echo "🔍 检查代码..."
	go vet ./...

# 显示帮助
.PHONY: help
help:
	@echo "Unity MCP Server 构建工具"
	@echo ""
	@echo "可用命令："
	@echo "  make all              - 完整构建（清理+依赖+编译）"
	@echo "  make deps             - 安装Go依赖"
	@echo "  make build            - 编译所有平台"
	@echo "  make build-windows    - 仅编译Windows版本"
	@echo "  make build-darwin     - 仅编译macOS版本"
	@echo "  make build-linux      - 仅编译Linux版本"
	@echo "  make run              - 本地运行开发版本"
	@echo "  make test             - 运行测试"
	@echo "  make clean            - 清理编译文件"
	@echo "  make fmt              - 格式化代码"
	@echo "  make lint             - 检查代码"
	@echo "  make help             - 显示此帮助"
	@echo ""
	@echo "编译后的文件位置："
	@echo "  Windows: ../bin/windows/$(BINARY_NAME).exe"
	@echo "  macOS:   ../bin/darwin/$(BINARY_NAME)"
	@echo "  Linux:   ../bin/linux/$(BINARY_NAME)"

# 版本信息
.PHONY: version
version:
	@echo "Unity MCP Server v$(VERSION)"
	@echo "Go version: $$(go version)"