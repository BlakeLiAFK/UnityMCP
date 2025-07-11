package main

/*
Only English logs are allowed.
*/

import (
	"encoding/json"
	"flag"
	"fmt"
	"log"
	"net/http"
	"os"
	"os/signal"
	"strconv"
	"syscall"
	"time"

	"github.com/mark3labs/mcp-go/mcp"
	"github.com/mark3labs/mcp-go/server"
)

// 服务器配置
type ServerConfig struct {
	Port      string
	UnityHost string
	UnityPort string
}

// 全局变量
var (
	config      ServerConfig
	unityClient *UnityTCPClient
	debugMode   bool
)

func main() {
	// 解析命令行参数
	var (
		port      = flag.String("port", "13000", "MCP server port")
		unityHost = flag.String("unity-host", "localhost", "Unity TCP server host")
		unityPort = flag.String("unity-port", "12000", "Unity TCP server port")
		debug     = flag.Bool("debug", false, "Enable debug mode with verbose logging")
	)
	flag.Parse()

	debugMode = *debug

	config = ServerConfig{
		Port:      *port,
		UnityHost: *unityHost,
		UnityPort: *unityPort,
	}

	// 初始化Unity TCP客户端
	unityClient = NewUnityTCPClient(config.UnityHost, config.UnityPort)

	// 创建MCP服务器
	mcpServer := server.NewMCPServer("unity-mcp-server", "1.0.0")

	// 注册工具处理器
	registerTools(mcpServer)

	// 创建SSE服务器 (mcp-go库自带完整的HTTP服务器)
	baseURL := fmt.Sprintf("http://localhost:%s", config.Port)
	sseServer := server.NewSSEServer(mcpServer, baseURL)

	// 创建辅助HTTP服务器用于管理端点 (/health, /tools)
	// 注: SSE服务器由mcp-go库管理，无法与其他HTTP端点合并到同一服务器
	// 这是因为mcp-go的SSEServer.Start()方法会创建并启动自己的HTTP服务器
	mux := http.NewServeMux()
	mux.HandleFunc("/health", withLogging(handleHealth, "/health"))
	mux.HandleFunc("/tools", withLogging(handleListTools, "/tools"))

	if debugMode {
		infoLog("Debug mode enabled")
	}

	// 计算管理端口 (SSE端口 + 1)
	managementPort := fmt.Sprintf("%d", mustParseInt(config.Port)+1)

	infoLog("Unity MCP server starting...")
	infoLog("Unity connection target: %s:%s", config.UnityHost, config.UnityPort)
	infoLog("Server architecture:")
	infoLog("  ┌─ Port %s (Main)", config.Port)
	infoLog("  └─ SSE /sse        - MCP SSE endpoint (managed by mcp-go library)")
	infoLog("  ┌─ Port %v (Management)", managementPort)
	infoLog("  ├─ GET /health     - Health check")
	infoLog("  └─ GET /tools      - Tool list")
	infoLog("")
	infoLog("Note: Due to limitations in the mcp-go library, the SSE server must run independently")

	// 设置优雅关闭
	go func() {
		c := make(chan os.Signal, 1)
		signal.Notify(c, os.Interrupt, syscall.SIGTERM)
		<-c
		infoLog("Received shutdown signal, shutting down server...")
		if unityClient != nil {
			unityClient.Close()
		}
		os.Exit(0)
	}()

	// 启动管理HTTP服务器在后台
	go func() {
		infoLog("Starting management HTTP server on port %s", managementPort)
		if err := http.ListenAndServe(":"+managementPort, mux); err != nil {
			errorLog("Management HTTP server error: %v", err)
		}
	}()

	// 启动SSE服务器 (这会阻塞)
	infoLog("Starting SSE server on port %s", config.Port)
	if err := sseServer.Start(":" + config.Port); err != nil {
		errorLog("Failed to start SSE server: %v", err)
		os.Exit(1)
	}
}

// 注册所有Unity工具
func registerTools(s *server.MCPServer) {
	// 注册脚本读取工具
	s.AddTool(
		mcp.NewTool("script_read",
			mcp.WithDescription("Read script file content from Unity project"),
			mcp.WithString("path", mcp.Description("Script file path to read (relative to Assets directory)"), mcp.Required()),
		),
		func(arguments map[string]interface{}) (*mcp.CallToolResult, error) {
			return callUnityTool("script_read", arguments)
		},
	)

	// 注册脚本写入工具
	s.AddTool(
		mcp.NewTool("script_write",
			mcp.WithDescription("Create or update script file in Unity project"),
			mcp.WithString("path", mcp.Description("Script file path (relative to Assets directory)"), mcp.Required()),
			mcp.WithString("content", mcp.Description("Script file content"), mcp.Required()),
			mcp.WithBoolean("overwrite", mcp.Description("Whether to overwrite existing file"), mcp.DefaultBool(true)),
		),
		func(arguments map[string]interface{}) (*mcp.CallToolResult, error) {
			return callUnityTool("script_write", arguments)
		},
	)

	// 注册场景获取工具
	s.AddTool(
		mcp.NewTool("scene_get",
			mcp.WithDescription("Get Unity current scene hierarchy data"),
			mcp.WithBoolean("includeComponents", mcp.Description("Whether to include component information"), mcp.DefaultBool(false)),
			mcp.WithBoolean("includeTransform", mcp.Description("Whether to include Transform information"), mcp.DefaultBool(true)),
		),
		func(arguments map[string]interface{}) (*mcp.CallToolResult, error) {
			return callUnityTool("scene_get", arguments)
		},
	)

	// 注册场景创建对象工具
	s.AddTool(
		mcp.NewTool("scene_create_object",
			mcp.WithDescription("Create new GameObject in Unity scene"),
			mcp.WithString("name", mcp.Description("GameObject name"), mcp.DefaultString("New GameObject")),
			mcp.WithNumber("parentId", mcp.Description("Parent object's InstanceID")),
		),
		func(arguments map[string]interface{}) (*mcp.CallToolResult, error) {
			return callUnityTool("scene_create_object", arguments)
		},
	)

	// 注册场景对象添加组件工具
	s.AddTool(
		mcp.NewTool("scene_object_add_component",
			mcp.WithDescription("Add component to GameObject in Unity scene"),
			mcp.WithNumber("instanceId", mcp.Description("GameObject's InstanceID"), mcp.Required()),
			mcp.WithString("componentType", mcp.Description("Component type name to add"), mcp.Required()),
		),
		func(arguments map[string]interface{}) (*mcp.CallToolResult, error) {
			return callUnityTool("scene_object_add_component", arguments)
		},
	)

	// 注册Transform获取工具
	s.AddTool(
		mcp.NewTool("scene_transform_get",
			mcp.WithDescription("Get Transform information of GameObject in Unity scene"),
			mcp.WithNumber("instanceId", mcp.Description("GameObject's InstanceID"), mcp.Required()),
			mcp.WithBoolean("worldSpace", mcp.Description("Whether to use world coordinate system"), mcp.DefaultBool(true)),
		),
		func(arguments map[string]interface{}) (*mcp.CallToolResult, error) {
			return callUnityTool("scene_transform_get", arguments)
		},
	)

	// 注册Transform设置工具
	s.AddTool(
		mcp.NewTool("scene_transform_set",
			mcp.WithDescription("Set Transform information of GameObject in Unity scene"),
			mcp.WithNumber("instanceId", mcp.Description("GameObject's InstanceID"), mcp.Required()),
		),
		func(arguments map[string]interface{}) (*mcp.CallToolResult, error) {
			return callUnityTool("scene_transform_set", arguments)
		},
	)

	// =================== UI工具 ===================
	
	// 注册UI RectTransform设置工具
	s.AddTool(
		mcp.NewTool("ui_rect_transform_set",
			mcp.WithDescription("Set UI element RectTransform properties (position, size, anchors)"),
			mcp.WithNumber("instanceId", mcp.Description("GameObject's InstanceID"), mcp.Required()),
		),
		func(arguments map[string]interface{}) (*mcp.CallToolResult, error) {
			return callUnityTool("ui_rect_transform_set", arguments)
		},
	)

	// 注册UI RectTransform获取工具
	s.AddTool(
		mcp.NewTool("ui_rect_transform_get",
			mcp.WithDescription("Get UI element RectTransform information"),
			mcp.WithNumber("instanceId", mcp.Description("GameObject's InstanceID"), mcp.Required()),
			mcp.WithBoolean("includeWorldSpace", mcp.Description("Whether to include world space information"), mcp.DefaultBool(true)),
		),
		func(arguments map[string]interface{}) (*mcp.CallToolResult, error) {
			return callUnityTool("ui_rect_transform_get", arguments)
		},
	)

	// 注册UI Image组件工具
	s.AddTool(
		mcp.NewTool("ui_image_set",
			mcp.WithDescription("Set UI Image component properties (sprite, color, material)"),
			mcp.WithNumber("instanceId", mcp.Description("GameObject's InstanceID"), mcp.Required()),
		),
		func(arguments map[string]interface{}) (*mcp.CallToolResult, error) {
			return callUnityTool("ui_image_set", arguments)
		},
	)

	// 注册UI Text组件工具
	s.AddTool(
		mcp.NewTool("ui_text_set",
			mcp.WithDescription("Set UI Text component properties (text content, font, color)"),
			mcp.WithNumber("instanceId", mcp.Description("GameObject's InstanceID"), mcp.Required()),
		),
		func(arguments map[string]interface{}) (*mcp.CallToolResult, error) {
			return callUnityTool("ui_text_set", arguments)
		},
	)

	// =================== 资源管理工具 ===================
	
	// 注册资源查找工具
	s.AddTool(
		mcp.NewTool("asset_find",
			mcp.WithDescription("Find project assets by conditions (path, type, name)"),
			mcp.WithString("path", mcp.Description("Search path relative to Assets directory"), mcp.DefaultString("Assets")),
			mcp.WithString("type", mcp.Description("Asset type name (Texture2D, AudioClip, etc.)")),
			mcp.WithString("name", mcp.Description("Asset name (supports wildcards)")),
			mcp.WithString("extension", mcp.Description("File extension")),
			mcp.WithBoolean("recursive", mcp.Description("Whether to search subdirectories"), mcp.DefaultBool(true)),
			mcp.WithNumber("maxResults", mcp.Description("Maximum number of results")),
		),
		func(arguments map[string]interface{}) (*mcp.CallToolResult, error) {
			return callUnityTool("asset_find", arguments)
		},
	)

	// 注册资源信息获取工具
	s.AddTool(
		mcp.NewTool("asset_get_info",
			mcp.WithDescription("Get detailed asset information (metadata, import settings)"),
			mcp.WithString("assetPath", mcp.Description("Asset path"), mcp.Required()),
			mcp.WithBoolean("includeMetadata", mcp.Description("Whether to include metadata"), mcp.DefaultBool(true)),
			mcp.WithBoolean("includeImportSettings", mcp.Description("Whether to include import settings"), mcp.DefaultBool(false)),
		),
		func(arguments map[string]interface{}) (*mcp.CallToolResult, error) {
			return callUnityTool("asset_get_info", arguments)
		},
	)

	// 注册资源依赖关系工具
	s.AddTool(
		mcp.NewTool("asset_get_dependencies",
			mcp.WithDescription("Get asset dependency relationships"),
			mcp.WithString("assetPath", mcp.Description("Asset path"), mcp.Required()),
			mcp.WithBoolean("recursive", mcp.Description("Whether to get dependencies recursively"), mcp.DefaultBool(false)),
			mcp.WithBoolean("includeImplicit", mcp.Description("Whether to include implicit dependencies"), mcp.DefaultBool(true)),
		),
		func(arguments map[string]interface{}) (*mcp.CallToolResult, error) {
			return callUnityTool("asset_get_dependencies", arguments)
		},
	)

	// 注册项目结构工具
	s.AddTool(
		mcp.NewTool("project_get_structure",
			mcp.WithDescription("Get project directory structure and statistics"),
			mcp.WithString("rootPath", mcp.Description("Root directory path"), mcp.DefaultString("Assets")),
			mcp.WithNumber("maxDepth", mcp.Description("Maximum directory depth")),
			mcp.WithBoolean("includeFiles", mcp.Description("Whether to include files"), mcp.DefaultBool(true)),
		),
		func(arguments map[string]interface{}) (*mcp.CallToolResult, error) {
			return callUnityTool("project_get_structure", arguments)
		},
	)

	// =================== 扩展Prefab工具 ===================
	
	// 注册预制体创建工具
	s.AddTool(
		mcp.NewTool("prefab_create",
			mcp.WithDescription("Create prefab from scene GameObject"),
			mcp.WithNumber("instanceId", mcp.Description("GameObject's InstanceID"), mcp.Required()),
			mcp.WithString("prefabPath", mcp.Description("Prefab save path"), mcp.Required()),
			mcp.WithBoolean("overwrite", mcp.Description("Whether to overwrite existing prefab"), mcp.DefaultBool(false)),
		),
		func(arguments map[string]interface{}) (*mcp.CallToolResult, error) {
			return callUnityTool("prefab_create", arguments)
		},
	)

	// 注册预制体信息工具
	s.AddTool(
		mcp.NewTool("prefab_get_info",
			mcp.WithDescription("Get detailed prefab information"),
			mcp.WithString("prefabPath", mcp.Description("Prefab asset path")),
			mcp.WithNumber("instanceId", mcp.Description("Prefab instance ID")),
			mcp.WithBoolean("includeInstances", mcp.Description("Whether to include scene instances"), mcp.DefaultBool(false)),
			mcp.WithBoolean("includeVariants", mcp.Description("Whether to include variant information"), mcp.DefaultBool(false)),
		),
		func(arguments map[string]interface{}) (*mcp.CallToolResult, error) {
			return callUnityTool("prefab_get_info", arguments)
		},
	)

	// 注册预制体修改工具
	s.AddTool(
		mcp.NewTool("prefab_modify",
			mcp.WithDescription("Manage prefab instance modifications"),
			mcp.WithNumber("instanceId", mcp.Description("Prefab instance ID"), mcp.Required()),
			mcp.WithString("operation", mcp.Description("Operation type (apply/revert/unpack/disconnect/check_overrides)"), mcp.Required()),
		),
		func(arguments map[string]interface{}) (*mcp.CallToolResult, error) {
			return callUnityTool("prefab_modify", arguments)
		},
	)

	// =================== 场景管理工具 ===================
	
	// 注册场景保存工具
	s.AddTool(
		mcp.NewTool("scene_save",
			mcp.WithDescription("Save current or specified scene"),
			mcp.WithString("scenePath", mcp.Description("Scene file path to save")),
			mcp.WithBoolean("saveAsNew", mcp.Description("Whether to save as new file"), mcp.DefaultBool(false)),
			mcp.WithBoolean("saveAll", mcp.Description("Whether to save all open scenes"), mcp.DefaultBool(false)),
		),
		func(arguments map[string]interface{}) (*mcp.CallToolResult, error) {
			return callUnityTool("scene_save", arguments)
		},
	)

	// 注册场景加载工具
	s.AddTool(
		mcp.NewTool("scene_load",
			mcp.WithDescription("Load specified scene file"),
			mcp.WithString("scenePath", mcp.Description("Scene file path to load"), mcp.Required()),
			mcp.WithString("loadMode", mcp.Description("Load mode (single/additive)"), mcp.DefaultString("single")),
			mcp.WithBoolean("saveCurrentScene", mcp.Description("Whether to save current scene before loading"), mcp.DefaultBool(true)),
		),
		func(arguments map[string]interface{}) (*mcp.CallToolResult, error) {
			return callUnityTool("scene_load", arguments)
		},
	)

	// 注册场景信息工具
	s.AddTool(
		mcp.NewTool("scene_get_info",
			mcp.WithDescription("Get detailed scene information"),
			mcp.WithString("scenePath", mcp.Description("Scene file path")),
			mcp.WithBoolean("includeObjects", mcp.Description("Whether to include object list"), mcp.DefaultBool(false)),
			mcp.WithBoolean("includeComponents", mcp.Description("Whether to include component analysis"), mcp.DefaultBool(false)),
			mcp.WithBoolean("analyzePerformance", mcp.Description("Whether to analyze performance"), mcp.DefaultBool(false)),
		),
		func(arguments map[string]interface{}) (*mcp.CallToolResult, error) {
			return callUnityTool("scene_get_info", arguments)
		},
	)

	// 注册场景对象查找工具
	s.AddTool(
		mcp.NewTool("scene_find_objects",
			mcp.WithDescription("Find GameObjects in scene by criteria"),
			mcp.WithString("name", mcp.Description("Object name to search for")),
			mcp.WithString("tag", mcp.Description("Object tag to filter by")),
			mcp.WithString("componentType", mcp.Description("Component type to filter by")),
			mcp.WithString("layer", mcp.Description("Layer name or number to filter by")),
			mcp.WithBoolean("activeOnly", mcp.Description("Whether to include only active objects"), mcp.DefaultBool(false)),
			mcp.WithBoolean("exactMatch", mcp.Description("Whether to use exact name matching"), mcp.DefaultBool(false)),
			mcp.WithNumber("maxResults", mcp.Description("Maximum number of results")),
			mcp.WithString("scenePath", mcp.Description("Scene path to search in")),
		),
		func(arguments map[string]interface{}) (*mcp.CallToolResult, error) {
			return callUnityTool("scene_find_objects", arguments)
		},
	)

	// 注册场景删除对象工具
	s.AddTool(
		mcp.NewTool("scene_delete_object",
			mcp.WithDescription("Delete GameObject from scene"),
			mcp.WithNumber("instanceId", mcp.Description("GameObject's InstanceID"), mcp.Required()),
			mcp.WithBoolean("deleteChildren", mcp.Description("Whether to delete children"), mcp.DefaultBool(true)),
		),
		func(arguments map[string]interface{}) (*mcp.CallToolResult, error) {
			return callUnityTool("scene_delete_object", arguments)
		},
	)

	// =================== 其他工具 ===================
	
	// 注册Editor日志工具
	s.AddTool(
		mcp.NewTool("editor_get_logs",
			mcp.WithDescription("Read Unity Editor Console logs"),
			mcp.WithNumber("maxLogs", mcp.Description("Maximum number of logs to retrieve")),
			mcp.WithString("logLevel", mcp.Description("Log level filter (all/error/warning/log/exception)"), mcp.DefaultString("all")),
			mcp.WithBoolean("clearLogs", mcp.Description("Whether to clear logs after reading"), mcp.DefaultBool(false)),
			mcp.WithBoolean("includeStackTrace", mcp.Description("Whether to include stack trace"), mcp.DefaultBool(false)),
		),
		func(arguments map[string]interface{}) (*mcp.CallToolResult, error) {
			return callUnityTool("editor_get_logs", arguments)
		},
	)
}

// 调用Unity工具的通用函数
func callUnityTool(toolName string, arguments map[string]interface{}) (*mcp.CallToolResult, error) {
	startTime := time.Now()
	requestId := fmt.Sprintf("mcp_%s_%d", toolName, time.Now().UnixNano())

	infoLog("=== TOOL CALL START ===")
	infoLog("Tool: %s", toolName)
	infoLog("Request ID: %s", requestId)
	infoLog("Arguments: %s", formatJSON(arguments))

	// 构造Unity消息
	unityMsg := map[string]interface{}{
		"action":    toolName,
		"params":    arguments,
		"id":        requestId,
		"timestamp": time.Now().UnixMilli(),
	}

	debugLog("Unity message payload: %s", formatJSON(unityMsg))

	// 发送到Unity，如果失败则重试
	var response map[string]interface{}
	var err error

	maxRetries := 3
	debugLog("Starting Unity communication with %d max retries", maxRetries)

	for i := 0; i < maxRetries; i++ {
		attemptStart := time.Now()
		debugLog("=== UNITY COMMUNICATION ATTEMPT %d/%d ===", i+1, maxRetries)
		debugLog("Tool: %s, Request ID: %s", toolName, requestId)
		debugLog("Attempt start time: %s", attemptStart.Format("15:04:05.000"))

		// 检查Unity客户端连接状态
		if unityClient != nil {
			if debugMode {
				isConnected := unityClient.IsConnected()
				debugLog("Unity client connection status: %t", isConnected)
				if !isConnected {
					debugLog("Unity client not connected, will attempt to connect during SendMessage")
				}
			}
		}

		response, err = unityClient.SendMessage(unityMsg)
		attemptDuration := time.Since(attemptStart)

		if err == nil {
			debugLog("=== UNITY COMMUNICATION SUCCESS ===")
			debugLog("Attempt %d succeeded in %v", i+1, attemptDuration)
			debugLog("Response size: %d bytes", len(formatJSON(response)))
			if debugMode {
				debugLog("Raw response preview: %.200s", formatJSON(response))
			}
			break
		}

		errorLog("=== UNITY COMMUNICATION FAILURE ===")
		errorLog("Attempt %d/%d failed for tool %s", i+1, maxRetries, toolName)
		errorLog("Attempt duration: %v", attemptDuration)
		errorLog("Error details: %s", err.Error())
		errorLog("Unity message that failed: %s", formatJSON(unityMsg))

		if i < maxRetries-1 {
			debugLog("Retrying in 1 second...")
			debugLog("Next attempt will be %d/%d", i+2, maxRetries)
			time.Sleep(time.Second)
		} else {
			errorLog("All %d attempts exhausted, giving up", maxRetries)
		}
	}

	totalDuration := time.Since(startTime)

	if err != nil {
		errorLog("Unity communication completely failed for tool %s after %d attempts (total time: %v): %s",
			toolName, maxRetries, totalDuration, err.Error())
		infoLog("=== TOOL CALL FAILED ===")
		return mcp.NewToolResultError(fmt.Sprintf("Unity communication failed after %d attempts: %s", maxRetries, err.Error())), nil
	}

	debugLog("Unity response received: %s", formatJSON(response))

	// 解析响应结构
	debugLog("=== RESPONSE ANALYSIS START ===")
	debugLog("Full response structure analysis:")

	var responseId, responseData, responseError interface{}
	responseKeys := make([]string, 0, len(response))
	for key := range response {
		responseKeys = append(responseKeys, key)
	}
	debugLog("Response contains keys: %v", responseKeys)

	if id, exists := response["id"]; exists {
		responseId = id
		debugLog("✓ Response ID found: %v (type: %T)", responseId, responseId)
	} else {
		debugLog("⚠ Response ID not found in response")
	}

	if data, exists := response["data"]; exists {
		responseData = data
		debugLog("✓ Response data found (type: %T)", responseData)
		if debugMode && responseData != nil {
			debugLog("Response data preview: %.500s", formatJSON(responseData))
		}
	} else {
		debugLog("⚠ Response data not found in response")
	}

	if errData, exists := response["error"]; exists {
		responseError = errData
		debugLog("⚠ Response error found: %v (type: %T)", responseError, responseError)
	} else {
		debugLog("✓ No error field in response")
	}

	// 检查success字段
	if success, exists := response["success"]; exists {
		debugLog("✓ Success field found: %v (type: %T)", success, success)
	} else {
		debugLog("⚠ Success field not found in response")
	}

	debugLog("=== RESPONSE ANALYSIS END ===")

	// 处理Unity响应
	debugLog("=== RESPONSE PROCESSING START ===")
	if success, ok := response["success"].(bool); ok && success {
		debugLog("✓ Success field validation passed: %t", success)

		data := response["data"]
		if data == nil {
			debugLog("⚠ Response data is nil, using empty map")
			data = map[string]interface{}{}
		} else {
			debugLog("✓ Response data is valid, type: %T", data)
		}

		infoLog("=== TOOL CALL SUCCESS ===")
		infoLog("Tool: %s", toolName)
		infoLog("Request ID: %s", requestId)
		infoLog("Total execution time: %v", totalDuration)
		infoLog("Success: Tool executed successfully")
		debugLog("Final response data: %s", formatJSON(data))

		// 创建结果文本
		resultText := fmt.Sprintf("Tool %s executed successfully:\n%s", toolName, formatJSON(data))
		debugLog("Result text length: %d characters", len(resultText))

		return mcp.NewToolResultText(resultText), nil
	} else {
		debugLog("✗ Success field validation failed")
		if !ok {
			debugLog("Success field type assertion failed, value: %v (type: %T)", response["success"], response["success"])
		} else {
			debugLog("Success field is false: %t", success)
		}

		errorMsg := "unknown error"
		if errStr, ok := response["error"].(string); ok {
			errorMsg = errStr
			debugLog("✓ Error message extracted from response: %s", errorMsg)
		} else {
			debugLog("⚠ Could not extract error message from response")
			if errField, exists := response["error"]; exists {
				debugLog("Error field exists but wrong type: %v (type: %T)", errField, errField)
				errorMsg = fmt.Sprintf("%v", errField)
			}
		}

		errorLog("=== TOOL CALL ERROR ===")
		errorLog("Tool: %s", toolName)
		errorLog("Request ID: %s", requestId)
		errorLog("Total execution time: %v", totalDuration)
		errorLog("Error: %s", errorMsg)
		debugLog("Full error response: %s", formatJSON(response))

		return mcp.NewToolResultError(fmt.Sprintf("Unity tool execution failed: %s", errorMsg)), nil
	}
}

// 健康检查
func handleHealth(w http.ResponseWriter, r *http.Request) {
	debugLog("Health check requested")

	// 检查Unity连接状态
	unityConnected := unityClient != nil && unityClient.IsConnected()

	status := map[string]interface{}{
		"status":         "healthy",
		"timestamp":      time.Now().Unix(),
		"unityHost":      config.UnityHost,
		"unityPort":      config.UnityPort,
		"unityConnected": unityConnected,
		"toolCount":      23,
		"debugMode":      debugMode,
		"version":        "1.0.0",
	}

	debugLog("Health status: %s", formatJSON(status))

	w.Header().Set("Content-Type", "application/json")
	if err := json.NewEncoder(w).Encode(status); err != nil {
		errorLog("Failed to encode health status: %v", err)
		http.Error(w, "Internal server error", http.StatusInternalServerError)
		return
	}

	debugLog("Health check response sent successfully")
}

// 列出可用工具
func handleListTools(w http.ResponseWriter, r *http.Request) {
	debugLog("Tools list requested")

	tools := []map[string]interface{}{
		// 基础工具
		{
			"name":        "script_read",
			"description": "Read script file content from Unity project",
			"category":    "file",
		},
		{
			"name":        "script_write",
			"description": "Create or update script file in Unity project",
			"category":    "file",
		},
		{
			"name":        "scene_get",
			"description": "Get Unity current scene hierarchy data",
			"category":    "scene",
		},
		{
			"name":        "scene_create_object",
			"description": "Create new GameObject in Unity scene",
			"category":    "scene",
		},
		{
			"name":        "scene_object_add_component",
			"description": "Add component to GameObject in Unity scene",
			"category":    "scene",
		},
		{
			"name":        "scene_transform_get",
			"description": "Get Transform information of GameObject in Unity scene",
			"category":    "transform",
		},
		{
			"name":        "scene_transform_set",
			"description": "Set Transform information of GameObject in Unity scene",
			"category":    "transform",
		},
		// UI工具
		{
			"name":        "ui_rect_transform_set",
			"description": "Set UI element RectTransform properties (position, size, anchors)",
			"category":    "ui",
		},
		{
			"name":        "ui_rect_transform_get",
			"description": "Get UI element RectTransform information",
			"category":    "ui",
		},
		{
			"name":        "ui_image_set",
			"description": "Set UI Image component properties (sprite, color, material)",
			"category":    "ui",
		},
		{
			"name":        "ui_text_set",
			"description": "Set UI Text component properties (text content, font, color)",
			"category":    "ui",
		},
		// 资源管理工具
		{
			"name":        "asset_find",
			"description": "Find project assets by conditions (path, type, name)",
			"category":    "asset",
		},
		{
			"name":        "asset_get_info",
			"description": "Get detailed asset information (metadata, import settings)",
			"category":    "asset",
		},
		{
			"name":        "asset_get_dependencies",
			"description": "Get asset dependency relationships",
			"category":    "asset",
		},
		{
			"name":        "project_get_structure",
			"description": "Get project directory structure and statistics",
			"category":    "project",
		},
		// 扩展Prefab工具
		{
			"name":        "prefab_create",
			"description": "Create prefab from scene GameObject",
			"category":    "prefab",
		},
		{
			"name":        "prefab_get_info",
			"description": "Get detailed prefab information",
			"category":    "prefab",
		},
		{
			"name":        "prefab_modify",
			"description": "Manage prefab instance modifications",
			"category":    "prefab",
		},
		// 场景管理工具
		{
			"name":        "scene_save",
			"description": "Save current or specified scene",
			"category":    "scene",
		},
		{
			"name":        "scene_load",
			"description": "Load specified scene file",
			"category":    "scene",
		},
		{
			"name":        "scene_get_info",
			"description": "Get detailed scene information",
			"category":    "scene",
		},
		{
			"name":        "scene_find_objects",
			"description": "Find GameObjects in scene by criteria",
			"category":    "scene",
		},
		{
			"name":        "scene_delete_object",
			"description": "Delete GameObject from scene",
			"category":    "scene",
		},
		// 其他工具
		{
			"name":        "editor_get_logs",
			"description": "Read Unity Editor Console logs",
			"category":    "editor",
		},
	}

	debugLog("Tools list: %d tools available", len(tools))
	if debugMode {
		for _, tool := range tools {
			debugLog("Tool: %s (%s) - %s", tool["name"], tool["category"], tool["description"])
		}
	}

	w.Header().Set("Content-Type", "application/json")
	if err := json.NewEncoder(w).Encode(tools); err != nil {
		errorLog("Failed to encode tools list: %v", err)
		http.Error(w, "Internal server error", http.StatusInternalServerError)
		return
	}

	debugLog("Tools list response sent successfully")
}

// 工具函数
func formatJSON(data interface{}) string {
	if data == nil {
		return "{}"
	}
	bytes, err := json.MarshalIndent(data, "", "  ")
	if err != nil {
		return fmt.Sprintf("%v", data)
	}
	return string(bytes)
}

func mustParseInt(s string) int {
	i, err := strconv.Atoi(s)
	if err != nil {
		log.Fatalf("Failed to parse port number: %s", s)
	}
	return i
}

// Debug日志函数
func debugLog(format string, args ...interface{}) {
	if debugMode {
		log.Printf("[DEBUG] "+format, args...)
	}
}

func infoLog(format string, args ...interface{}) {
	log.Printf("[INFO] "+format, args...)
}

func errorLog(format string, args ...interface{}) {
	log.Printf("[ERROR] "+format, args...)
}

// HTTP日志中间件
func withLogging(handler http.HandlerFunc, endpoint string) http.HandlerFunc {
	return func(w http.ResponseWriter, r *http.Request) {
		start := time.Now()

		// 记录请求入口
		debugLog("HTTP [%s] %s %s - Client: %s, User-Agent: %s",
			r.Method, endpoint, r.URL.RawQuery, r.RemoteAddr, r.UserAgent())

		if debugMode {
			// 记录请求头
			for name, values := range r.Header {
				for _, value := range values {
					debugLog("HTTP [%s] %s - Header: %s = %s", r.Method, endpoint, name, value)
				}
			}
		}

		// 包装ResponseWriter来捕获状态码
		wrapped := &responseWriter{ResponseWriter: w, statusCode: http.StatusOK}

		// 执行处理器
		handler(wrapped, r)

		// 记录请求出口
		duration := time.Since(start)
		infoLog("HTTP [%s] %s - Status: %d, Duration: %v",
			r.Method, endpoint, wrapped.statusCode, duration)
	}
}

// 响应写入器包装器
type responseWriter struct {
	http.ResponseWriter
	statusCode int
}

func (rw *responseWriter) WriteHeader(code int) {
	rw.statusCode = code
	rw.ResponseWriter.WriteHeader(code)
}
