using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.Collections;
using System.Threading.Tasks;
using System.Linq;

public class UnityMCPWindow : EditorWindow
{
    private static readonly string TCP_PORT_PREF_KEY = "UnityMCP_TCPPort";
    private static readonly string MCP_PORT_PREF_KEY = "UnityMCP_MCPPort";
    private static readonly string DEBUG_MODE_PREF_KEY = "UnityMCP_DebugMode";
    private static readonly string MCP_SERVER_PATH_PREF_KEY = "UnityMCP_ServerPath";
    private static readonly string TCP_ONLY_MODE_PREF_KEY = "UnityMCP_TCPOnlyMode";

    // 服务器配置
    private int tcpPort = 12000; // Unity TCP服务器端口
    private int mcpPort = 13000; // Go MCP服务器端口
    private bool debugMode = false; // Debug模式
    private bool tcpOnlyMode = false; // 只启动TCP服务器模式
    private string mcpServerPath = ""; // MCP服务器可执行文件路径

    // 服务器实例
    private MCPServer tcpServer; // Unity TCP服务器
    private MCPMessageDispatcher dispatcher; // 消息分发器
    private MCPServerManager mcpManager; // Go MCP服务器管理器

    // 服务器状态
    private bool isServerRunning = false; // 整体服务器运行状态
    private bool isTcpServerRunning = false; // TCP服务器状态
    private bool isMcpServerRunning = false; // MCP服务器状态
    private int connectedClients = 0; // TCP连接的客户端数量

    // UI相关
    private Vector2 scrollPosition;
    private int selectedTab = 0;
    private readonly string[] tabNames = { "服务器管理", "AI工具配置", "工具列表", "日志" };

    // 日志相关
    private List<string> logMessages = new List<string>();
    private Vector2 logScrollPosition;

    // AI工具配置模板
    private readonly Dictionary<string, string> aiToolConfigs = new Dictionary<string, string>();

    [MenuItem("Window/UnityMCP")]
    public static void ShowWindow()
    {
        var window = GetWindow<UnityMCPWindow>("UnityMCP");
        window.minSize = new Vector2(600, 400);
    }

    private void OnEnable()
    {
        try
        {
            // 加载保存的配置
            tcpPort = EditorPrefs.GetInt(TCP_PORT_PREF_KEY, 12000);
            mcpPort = EditorPrefs.GetInt(MCP_PORT_PREF_KEY, 13000);
            debugMode = EditorPrefs.GetBool(DEBUG_MODE_PREF_KEY, false);
            tcpOnlyMode = EditorPrefs.GetBool(TCP_ONLY_MODE_PREF_KEY, false);

            // 加载MCP服务器路径，如果没有保存的配置则使用默认路径
            mcpServerPath = EditorPrefs.GetString(MCP_SERVER_PATH_PREF_KEY, "");
            if (string.IsNullOrEmpty(mcpServerPath))
            {
                mcpServerPath = GetDefaultMCPServerPath();
            }

            // 初始化Unity TCP服务器
            if (tcpServer == null)
            {
                tcpServer = new MCPServer();
                tcpServer.onClientConnected += OnClientConnected;
                tcpServer.onClientDisconnected += OnClientDisconnected;
            }

            // 初始化消息分发器
            if (dispatcher == null)
            {
                dispatcher = new MCPMessageDispatcher(tcpServer);
            }

            // 初始化Go MCP服务器管理器
            if (mcpManager == null)
            {
                mcpManager = new MCPServerManager();
                mcpManager.OnStatusChanged += OnMCPStatusChanged;
                mcpManager.OnLogMessage += OnMCPLogMessage;
            }

            // 确保MCP管理器有正确的可执行路径
            mcpManager.ExecutablePath = mcpServerPath;

            // 初始化AI工具配置
            InitializeAIToolConfigs();

            // 更新连接数和状态
            UpdateClientCount();
            UpdateServerStatus();

            AddLogMessage("Unity MCP窗口已初始化");
        }
        catch (System.Exception e)
        {
            AddLogMessage($"初始化失败: {e.Message}");
            Debug.LogException(e);
        }
    }

    private void OnGUI()
    {
        EditorGUILayout.Space(10);

        // 标签页选择
        selectedTab = GUILayout.Toolbar(selectedTab, tabNames);
        EditorGUILayout.Space(10);

        switch (selectedTab)
        {
            case 0:
                DrawServerManagementTab();
                break;
            case 1:
                DrawAIToolConfigTab();
                break;
            case 2:
                DrawToolListTab();
                break;
            case 3:
                DrawLogTab();
                break;
        }
    }

    /// <summary>
    /// 绘制服务器管理标签页
    /// </summary>
    private void DrawServerManagementTab()
    {
        EditorGUILayout.LabelField("Unity MCP服务器管理", EditorStyles.boldLabel);
        EditorGUILayout.Space(10);

        // 服务器配置区域
        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField("服务器配置", EditorStyles.boldLabel);
        EditorGUILayout.Space(5);

        // 只有在服务器停止时才能编辑端口
        GUI.enabled = !isServerRunning;

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("TCP端口:", GUILayout.Width(80));
        tcpPort = EditorGUILayout.IntField(tcpPort);
        tcpPort = Mathf.Clamp(tcpPort, 1024, 65535);
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("MCP端口:", GUILayout.Width(80));
        mcpPort = EditorGUILayout.IntField(mcpPort);
        mcpPort = Mathf.Clamp(mcpPort, 1024, 65535);
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Debug模式:", GUILayout.Width(80));
        debugMode = EditorGUILayout.Toggle(debugMode);
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("仅TCP模式:", GUILayout.Width(80));
        tcpOnlyMode = EditorGUILayout.Toggle(tcpOnlyMode);
        EditorGUILayout.EndHorizontal();

        // MCP服务器路径配置
        EditorGUILayout.BeginVertical();
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("MCP服务器:", GUILayout.Width(80));
        EditorGUILayout.LabelField(string.IsNullOrEmpty(mcpServerPath) ? "未设置" : System.IO.Path.GetFileName(mcpServerPath), EditorStyles.textField);
        if (GUILayout.Button("选择", GUILayout.Width(50)))
        {
            SelectMCPServerPath();
        }
        if (GUILayout.Button("重置", GUILayout.Width(50)))
        {
            mcpServerPath = GetDefaultMCPServerPath();

            // 立即更新MCPServerManager
            if (mcpManager != null)
            {
                mcpManager.ExecutablePath = mcpServerPath;
            }

            AddLogMessage($"MCP服务器路径已重置为默认值: {mcpServerPath}");

            // 立即保存设置
            EditorPrefs.SetString(MCP_SERVER_PATH_PREF_KEY, mcpServerPath);
        }
        EditorGUILayout.EndHorizontal();

        // 显示完整路径（只读）
        if (!string.IsNullOrEmpty(mcpServerPath))
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("路径:", GUILayout.Width(80));
            EditorGUILayout.SelectableLabel(mcpServerPath, EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
            EditorGUILayout.EndHorizontal();

            // 检查文件是否存在
            if (!System.IO.File.Exists(mcpServerPath))
            {
                EditorGUILayout.HelpBox("⚠️ 文件不存在，请重新选择", MessageType.Warning);
            }
        }
        EditorGUILayout.EndVertical();

        GUI.enabled = true;

        EditorGUILayout.Space(5);

        // 显示当前配置
        EditorGUILayout.LabelField($"Unity TCP服务器: localhost:{tcpPort}");
        if (!tcpOnlyMode)
        {
            EditorGUILayout.LabelField($"MCP SSE服务器: localhost:{mcpPort}");
        }
        EditorGUILayout.LabelField($"Debug模式: {(debugMode ? "启用" : "禁用")}");
        EditorGUILayout.LabelField($"运行模式: {(tcpOnlyMode ? "仅TCP模式" : "完整MCP模式")}");
        if (!tcpOnlyMode)
        {
            EditorGUILayout.LabelField($"MCP服务器: {(System.IO.File.Exists(mcpServerPath) ? "✓ 已配置" : "✗ 未找到")}");
        }

        EditorGUILayout.EndVertical();

        EditorGUILayout.Space(10);

        // 服务器控制区域
        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField("服务器控制", EditorStyles.boldLabel);
        EditorGUILayout.Space(5);

        // 主控制按钮
        EditorGUILayout.BeginHorizontal();

        // 检查是否可以启动服务器
        // 如果是仅TCP模式，不需要检查MCP服务器路径
        bool canStartServer = !isServerRunning && (tcpOnlyMode || (!string.IsNullOrEmpty(mcpServerPath) && System.IO.File.Exists(mcpServerPath)));

        if (!isServerRunning)
        {
            GUI.enabled = canStartServer;
            string buttonText = tcpOnlyMode ? "启动TCP服务器" : "启动MCP服务器";
            if (GUILayout.Button(buttonText, GUILayout.Height(30)))
            {
                StartServers();
            }
            GUI.enabled = true;
        }
        else
        {
            string stopButtonText = tcpOnlyMode ? "停止TCP服务器" : "停止MCP服务器";
            if (GUILayout.Button(stopButtonText, GUILayout.Height(30)))
            {
                StopServers();
            }
        }

        if (GUILayout.Button("保存配置", GUILayout.Width(100), GUILayout.Height(30)))
        {
            SaveSettings();
        }

        EditorGUILayout.EndHorizontal();

        // 如果不能启动，显示原因
        if (!canStartServer && !isServerRunning)
        {
            if (!tcpOnlyMode)
            {
                if (string.IsNullOrEmpty(mcpServerPath))
                {
                    EditorGUILayout.HelpBox("请先选择MCP服务器可执行文件", MessageType.Warning);
                }
                else if (!System.IO.File.Exists(mcpServerPath))
                {
                    EditorGUILayout.HelpBox("MCP服务器文件不存在", MessageType.Error);
                }
            }
        }

        EditorGUILayout.Space(10);

        // 服务器状态显示
        DrawServerStatus();

        EditorGUILayout.EndVertical();

        EditorGUILayout.Space(10);

        // 快速操作区域
        if (isServerRunning)
        {
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("快速访问", EditorStyles.boldLabel);
            EditorGUILayout.Space(5);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"SSE端点: http://localhost:{mcpPort}/sse");
            if (GUILayout.Button("复制", GUILayout.Width(50)))
            {
                EditorGUIUtility.systemCopyBuffer = $"http://localhost:{mcpPort}/sse";
                AddLogMessage("SSE端点地址已复制到剪贴板");
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("打开健康检查"))
            {
                Application.OpenURL($"http://localhost:{mcpPort + 1}/health");
            }
            if (GUILayout.Button("查看工具列表"))
            {
                Application.OpenURL($"http://localhost:{mcpPort + 1}/tools");
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }

        // 调试信息 (仅在Debug模式下显示)
        if (debugMode)
        {
            EditorGUILayout.Space(5);
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("调试信息", EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"默认MCP路径: {GetDefaultMCPServerPath()}");
            EditorGUILayout.LabelField($"当前平台: {Application.platform}");
            if (mcpManager != null)
            {
                EditorGUILayout.LabelField($"管理器可执行路径: {mcpManager.ExecutablePath}");
            }
            EditorGUILayout.EndVertical();
        }
    }

    /// <summary>
    /// 绘制服务器状态
    /// </summary>
    private void DrawServerStatus()
    {
        EditorGUILayout.LabelField("服务器状态", EditorStyles.boldLabel);
        EditorGUILayout.Space(3);

        // TCP服务器状态
        string tcpStatus = isTcpServerRunning ?
            $"<color=green>✓ TCP服务器运行中 (端口: {tcpPort}, 客户端: {connectedClients})</color>" :
            "<color=red>✗ TCP服务器未运行</color>";
        EditorGUILayout.LabelField("TCP服务器:", new GUIStyle(EditorStyles.label) { richText = true });
        EditorGUILayout.LabelField(tcpStatus, new GUIStyle(EditorStyles.label) { richText = true });

        EditorGUILayout.Space(3);

        // MCP服务器状态（仅在非TCP模式下显示）
        if (!tcpOnlyMode)
        {
            string mcpStatus = GetMCPStatusText();
            EditorGUILayout.LabelField("MCP服务器:", new GUIStyle(EditorStyles.label) { richText = true });
            EditorGUILayout.LabelField(mcpStatus, new GUIStyle(EditorStyles.label) { richText = true });
        }
        else
        {
            EditorGUILayout.LabelField("MCP服务器:", new GUIStyle(EditorStyles.label) { richText = true });
            EditorGUILayout.LabelField("<color=grey>仅TCP模式 - 已禁用</color>", new GUIStyle(EditorStyles.label) { richText = true });
        }

        EditorGUILayout.Space(5);

        // 整体状态提示
        if (isServerRunning && isTcpServerRunning && isMcpServerRunning)
        {
            if (tcpOnlyMode)
            {
                EditorGUILayout.HelpBox("✅ TCP服务器已就绪，可以接收Unity工具调用", MessageType.Info);
            }
            else
            {
                EditorGUILayout.HelpBox("✅ MCP服务器已就绪，可以连接AI工具", MessageType.Info);
            }
        }
        else if (isServerRunning)
        {
            EditorGUILayout.HelpBox("⏳ 服务器启动中，请稍候...", MessageType.Warning);
        }
        else
        {
            EditorGUILayout.HelpBox("⏹ 服务器未运行", MessageType.None);
        }
    }

    /// <summary>
    /// 绘制AI工具配置标签页
    /// </summary>
    private void DrawAIToolConfigTab()
    {
        EditorGUILayout.LabelField("MCP连接配置", EditorStyles.boldLabel);
        EditorGUILayout.Space(5);

        // 显示连接状态
        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField("服务器状态", EditorStyles.boldLabel);
        EditorGUILayout.Space(5);

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("TCP服务器:", GUILayout.Width(100));
        EditorGUILayout.LabelField(isTcpServerRunning ? "✅ 运行中" : "❌ 未运行", isTcpServerRunning ? EditorStyles.helpBox : EditorStyles.helpBox);
        EditorGUILayout.EndHorizontal();

        if (!tcpOnlyMode)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("MCP服务器:", GUILayout.Width(100));
            EditorGUILayout.LabelField(isMcpServerRunning ? "✅ 运行中" : "❌ 未运行", isMcpServerRunning ? EditorStyles.helpBox : EditorStyles.helpBox);
            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("连接客户端:", GUILayout.Width(100));
        EditorGUILayout.LabelField($"{connectedClients} 个", EditorStyles.helpBox);
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.EndVertical();

        EditorGUILayout.Space(10);

        // MCP连接配置信息
        if (isServerRunning)
        {
            EditorGUILayout.LabelField("MCP连接配置", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("复制以下配置信息到AI工具的MCP设置中", MessageType.Info);
            EditorGUILayout.Space(5);

            // SSE连接配置
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("SSE连接 (推荐)", EditorStyles.boldLabel);
            string sseUrl = $"http://localhost:{mcpPort}/sse";
            
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("SSE URL:", GUILayout.Width(80));
            EditorGUILayout.SelectableLabel(sseUrl, EditorStyles.textField);
            if (GUILayout.Button("复制", GUILayout.Width(60)))
            {
                EditorGUIUtility.systemCopyBuffer = sseUrl;
                AddLogMessage("已复制SSE URL到剪贴板");
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(5);

            // TCP连接配置
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("TCP连接", EditorStyles.boldLabel);
            
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("主机:", GUILayout.Width(80));
            EditorGUILayout.SelectableLabel("localhost", EditorStyles.textField);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("端口:", GUILayout.Width(80));
            EditorGUILayout.SelectableLabel(tcpPort.ToString(), EditorStyles.textField);
            if (GUILayout.Button("复制", GUILayout.Width(60)))
            {
                EditorGUIUtility.systemCopyBuffer = tcpPort.ToString();
                AddLogMessage("已复制TCP端口到剪贴板");
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(5);

            // 健康检查URL
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("管理接口", EditorStyles.boldLabel);
            string healthUrl = $"http://localhost:{mcpPort + 1}/health";
            string toolsUrl = $"http://localhost:{mcpPort + 1}/tools";

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("健康检查:", GUILayout.Width(80));
            EditorGUILayout.SelectableLabel(healthUrl, EditorStyles.textField);
            if (GUILayout.Button("复制", GUILayout.Width(60)))
            {
                EditorGUIUtility.systemCopyBuffer = healthUrl;
                AddLogMessage("已复制健康检查URL到剪贴板");
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("工具列表:", GUILayout.Width(80));
            EditorGUILayout.SelectableLabel(toolsUrl, EditorStyles.textField);
            if (GUILayout.Button("复制", GUILayout.Width(60)))
            {
                EditorGUIUtility.systemCopyBuffer = toolsUrl;
                AddLogMessage("已复制工具列表URL到剪贴板");
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
        }
        else
        {
            EditorGUILayout.HelpBox("请先启动服务器以获取连接配置", MessageType.Warning);
        }
    }

    /// <summary>
    /// 绘制工具列表标签页
    /// </summary>
    private void DrawToolListTab()
    {
        EditorGUILayout.LabelField("已实现的Unity MCP工具", EditorStyles.boldLabel);
        EditorGUILayout.Space(5);

        // 工具统计信息
        var toolCategories = GetToolCategories();
        int totalTools = toolCategories.Values.Sum(list => list.Count);

        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField($"工具总数: {totalTools}", EditorStyles.boldLabel);
        EditorGUILayout.LabelField($"分类数量: {toolCategories.Count}", EditorStyles.label);
        EditorGUILayout.EndVertical();

        EditorGUILayout.Space(5);

        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.Height(350));

        foreach (var category in toolCategories)
        {
            EditorGUILayout.BeginVertical("box");
            
            // 分类标题
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"📁 {category.Key} ({category.Value.Count})", EditorStyles.boldLabel);
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.Space(3);

            // 工具列表
            foreach (var tool in category.Value)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.Space(15);
                EditorGUILayout.BeginVertical();
                
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"🔧 {tool.Name}", EditorStyles.label, GUILayout.Width(200));
                if (GUILayout.Button("复制名称", GUILayout.Width(80)))
                {
                    EditorGUIUtility.systemCopyBuffer = tool.Name;
                    AddLogMessage($"已复制工具名称: {tool.Name}");
                }
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.LabelField(tool.Description, EditorStyles.wordWrappedMiniLabel);
                
                EditorGUILayout.EndVertical();
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.Space(2);
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(5);
        }

        EditorGUILayout.EndScrollView();

        // 底部操作按钮
        EditorGUILayout.Space(5);
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("刷新工具列表"))
        {
            AddLogMessage("工具列表已刷新");
        }
        if (GUILayout.Button("导出工具清单"))
        {
            ExportToolList();
        }
        EditorGUILayout.EndHorizontal();
    }

    /// <summary>
    /// 绘制日志标签页
    /// </summary>
    private void DrawLogTab()
    {
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("系统日志", EditorStyles.boldLabel);
        if (GUILayout.Button("清除日志", GUILayout.Width(80)))
        {
            logMessages.Clear();
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(5);

        logScrollPosition = EditorGUILayout.BeginScrollView(logScrollPosition, GUILayout.Height(350));

        foreach (string message in logMessages)
        {
            EditorGUILayout.LabelField(message, EditorStyles.wordWrappedLabel);
        }

        EditorGUILayout.EndScrollView();
    }

    /// <summary>
    /// 更新服务器状态
    /// </summary>
    private void UpdateServerStatus()
    {
        // 检查TCP服务器状态
        if (tcpServer != null)
        {
            isTcpServerRunning = (tcpServer.status == MCPServer.Status.Running);
        }

        // 检查MCP服务器状态（仅在非TCP模式下）
        if (!tcpOnlyMode && mcpManager != null)
        {
            isMcpServerRunning = (mcpManager.Status == MCPServerManager.ServerStatus.Running);
        }
        else if (tcpOnlyMode)
        {
            // 在仅TCP模式下，MCP服务器状态设为true（表示"不需要MCP服务器"）
            isMcpServerRunning = true;
        }

        // 更新整体状态
        isServerRunning = isTcpServerRunning && isMcpServerRunning;
    }

    /// <summary>
    /// 启动所有服务器 (异步版本)
    /// </summary>
    private async void StartServers()
    {
        if (isServerRunning)
        {
            AddLogMessage("服务器已在运行中");
            return;
        }

        // 如果不是仅TCP模式，检查MCP服务器可执行文件是否存在
        if (!tcpOnlyMode && (string.IsNullOrEmpty(mcpServerPath) || !System.IO.File.Exists(mcpServerPath)))
        {
            AddLogMessage("✗ MCP服务器可执行文件不存在，请先配置正确的路径");
            EditorUtility.DisplayDialog("错误", "MCP服务器可执行文件不存在，请在服务器配置中选择正确的文件路径。", "确定");
            return;
        }

        string serverTypeText = tcpOnlyMode ? "TCP服务器" : "MCP服务器";
        AddLogMessage($"开始启动{serverTypeText}...");
        isServerRunning = true;

        // 更新配置（仅在非TCP模式时更新MCP管理器）
        if (!tcpOnlyMode)
        {
            mcpManager.MCPPort = mcpPort.ToString();
            mcpManager.UnityPort = tcpPort.ToString();
            mcpManager.DebugMode = debugMode;
            mcpManager.ExecutablePath = mcpServerPath;
        }

        try
        {
            await StartServersAsync();
        }
        catch (System.Exception e)
        {
            AddLogMessage($"启动服务器时发生错误: {e.Message}");
            StopServers();
        }
    }

    /// <summary>
    /// 异步启动服务器流程
    /// </summary>
    private async Task StartServersAsync()
    {
        bool success = true;

        // 1. 启动Unity TCP服务器
        AddLogMessage($"启动Unity TCP服务器 (端口: {tcpPort})...");
        if (tcpServer != null && tcpServer.status == MCPServer.Status.Stopped)
        {
            try
            {
                tcpServer.StartServer(tcpPort);
                isTcpServerRunning = (tcpServer.status == MCPServer.Status.Running);

                if (isTcpServerRunning)
                {
                    AddLogMessage("✓ Unity TCP服务器启动成功");
                }
                else
                {
                    AddLogMessage("✗ Unity TCP服务器启动失败");
                    success = false;
                }
            }
            catch (System.Exception e)
            {
                AddLogMessage($"✗ Unity TCP服务器启动异常: {e.Message}");
                success = false;
            }
        }

        if (!success) return;

        // 仅TCP模式下，跳过MCP服务器启动
        if (tcpOnlyMode)
        {
            isMcpServerRunning = true; // 设置为true以便整体状态正确
            AddLogMessage("✓ 仅TCP模式，跳过MCP服务器启动");
            AddLogMessage($"🎉 TCP服务器已就绪! 监听端口: {tcpPort}");
        }
        else
        {
            // 等待1秒
            await Task.Delay(1000);

            // 2. 启动Go MCP服务器
            AddLogMessage($"启动Go MCP服务器 (端口: {mcpPort})...");
            if (mcpManager.StartMCPServer())
            {
                // 等待5秒检查MCP服务器状态
                int waitTime = 0;
                while (waitTime < 5000 && mcpManager.Status != MCPServerManager.ServerStatus.Running)
                {
                    await Task.Delay(500);
                    waitTime += 500;

                    // 主线程更新UI
                    EditorApplication.delayCall += () => Repaint();
                }

                if (mcpManager.Status == MCPServerManager.ServerStatus.Running)
                {
                    isMcpServerRunning = true;
                    AddLogMessage("✓ Go MCP服务器启动成功");
                    AddLogMessage($"🎉 MCP服务器已就绪! 访问地址: http://localhost:{mcpPort}");
                }
                else
                {
                    AddLogMessage("✗ Go MCP服务器5秒内未能启动，可能端口被占用");
                    success = false;
                }
            }
            else
            {
                AddLogMessage("✗ Go MCP服务器启动失败");
                success = false;
            }
        }

        // 3. 如果启动失败，清理所有服务器
        if (!success)
        {
            AddLogMessage("⚠️ 服务器启动失败，正在清理...");
            EditorApplication.delayCall += () =>
            {
                StopServers();
                AddLogMessage("💡 建议修改端口配置后重试");
                Repaint();
            };
        }
        else
        {
            // 启动成功，更新UI
            EditorApplication.delayCall += () => Repaint();
        }
    }

    /// <summary>
    /// 停止所有服务器
    /// </summary>
    private void StopServers()
    {
        string serverTypeText = tcpOnlyMode ? "TCP服务器" : "MCP服务器";
        AddLogMessage($"正在停止{serverTypeText}...");

        // 停止Go MCP服务器（仅在非TCP模式下）
        if (!tcpOnlyMode && mcpManager != null)
        {
            mcpManager.StopMCPServer();
        }
        isMcpServerRunning = false;

        // 停止Unity TCP服务器
        if (tcpServer != null && tcpServer.status == MCPServer.Status.Running)
        {
            tcpServer.StopServer();
            isTcpServerRunning = false;
            connectedClients = 0;
        }

        isServerRunning = false;
        AddLogMessage($"✓ {serverTypeText}已停止");
        Repaint();
    }

    /// <summary>
    /// 初始化AI工具配置模板
    /// </summary>
    private void InitializeAIToolConfigs()
    {
        string sseUrl = $"http://localhost:{mcpPort}/sse";
        string managementUrl = $"http://localhost:{mcpPort}";

        // Claude Code配置 (SSE)
        aiToolConfigs["Claude Code"] = $@"{{
  ""mcpServers"": {{
    ""unity"": {{
      ""url"": ""{sseUrl}"",
      ""type"": ""sse"",
      ""name"": ""Unity MCP Server"",
      ""description"": ""Unity编辑器工具集成""
    }}
  }}
}}";

        // Cursor配置 (SSE)
        aiToolConfigs["Cursor"] = $@"{{
  ""mcp"": {{
    ""servers"": [
      {{
        ""name"": ""unity"",
        ""url"": ""{sseUrl}"",
        ""type"": ""sse"",
        ""description"": ""Unity编辑器集成""
      }}
    ]
  }}
}}";

        // Windsurf配置 (SSE)
        aiToolConfigs["Windsurf"] = $@"# Windsurf MCP配置
# 在 .windsurf/settings.json 中添加:
{{
  ""mcp.servers"": [
    {{
      ""name"": ""unity"",
      ""endpoint"": ""{sseUrl}"",
      ""transport"": ""sse"",
      ""capabilities"": [""tools""]
    }}
  ]
}}";

        // VSCode配置 (SSE)
        aiToolConfigs["VSCode"] = $@"// VSCode MCP插件配置
// 在 settings.json 中添加:
{{
  ""mcp.servers"": {{
    ""unity"": {{
      ""endpoint"": ""{sseUrl}"",
      ""type"": ""sse"",
      ""name"": ""Unity MCP Server"",
      ""description"": ""Unity编辑器工具集成""
    }}
  }}
}}";

        // 直接连接示例
        aiToolConfigs["直接连接示例"] = $@"# SSE连接示例
连接到: {sseUrl}

# 管理端点 (用于状态检查):
健康检查: {managementUrl}/health
工具列表: {managementUrl}/tools

# 注意: SSE连接需要支持Server-Sent Events的客户端";

        // 测试和调试
        aiToolConfigs["测试和调试"] = $@"# 测试命令
# 健康检查:
curl {managementUrl}/health

# 获取工具列表:
curl {managementUrl}/tools

# SSE连接测试:
curl -N -H ""Accept: text/event-stream"" {sseUrl}

# 启用debug模式:
在Unity界面勾选""Debug模式""复选框，然后重启服务器";
    }

    /// <summary>
    /// 获取MCP状态文本
    /// </summary>
    private string GetMCPStatusText()
    {
        if (mcpManager == null) return "<color=grey>MCP管理器未初始化</color>";

        switch (mcpManager.Status)
        {
            case MCPServerManager.ServerStatus.Stopped:
                return "<color=red>✗ MCP服务器未运行</color>";
            case MCPServerManager.ServerStatus.Starting:
                return "<color=yellow>⏳ MCP服务器启动中...</color>";
            case MCPServerManager.ServerStatus.Running:
                return $"<color=green>✓ MCP服务器运行中 (端口: {mcpPort})</color>";
            case MCPServerManager.ServerStatus.Error:
                return $"<color=red>✗ MCP服务器错误: {mcpManager.ErrorMessage}</color>";
            default:
                return "<color=grey>MCP服务器状态未知</color>";
        }
    }

    /// <summary>
    /// 保存设置
    /// </summary>
    private void SaveSettings()
    {
        EditorPrefs.SetInt(TCP_PORT_PREF_KEY, tcpPort);
        EditorPrefs.SetInt(MCP_PORT_PREF_KEY, mcpPort);
        EditorPrefs.SetBool(DEBUG_MODE_PREF_KEY, debugMode);
        EditorPrefs.SetBool(TCP_ONLY_MODE_PREF_KEY, tcpOnlyMode);
        EditorPrefs.SetString(MCP_SERVER_PATH_PREF_KEY, mcpServerPath);
        AddLogMessage($"配置已保存 - TCP端口: {tcpPort}, MCP端口: {mcpPort}, Debug: {(debugMode ? "启用" : "禁用")}, 仅TCP模式: {(tcpOnlyMode ? "启用" : "禁用")}, 服务器路径: {mcpServerPath}");
    }

    /// <summary>
    /// 添加日志消息
    /// </summary>
    private void AddLogMessage(string message)
    {
        string timestamp = System.DateTime.Now.ToString("HH:mm:ss");
        logMessages.Add($"[{timestamp}] {message}");

        // 限制日志数量
        if (logMessages.Count > 100)
        {
            logMessages.RemoveAt(0);
        }

        // 如果在日志标签页，自动刷新
        if (selectedTab == 3)
        {
            Repaint();
        }
    }

    /// <summary>
    /// 获取默认的MCP服务器可执行文件路径
    /// </summary>
    private string GetDefaultMCPServerPath()
    {
        string baseDir = System.IO.Path.Combine(Application.dataPath, "Editor", "UnityMCP", "bin");

        // 根据平台确定可执行文件路径
        switch (Application.platform)
        {
            case RuntimePlatform.WindowsEditor:
                return System.IO.Path.Combine(baseDir, "windows", "unity-mcp-server.exe");
            case RuntimePlatform.OSXEditor:
                return System.IO.Path.Combine(baseDir, "darwin", "unity-mcp-server");
            case RuntimePlatform.LinuxEditor:
                return System.IO.Path.Combine(baseDir, "linux", "unity-mcp-server");
            default:
                return System.IO.Path.Combine(baseDir, "unity-mcp-server");
        }
    }

    /// <summary>
    /// 选择MCP服务器可执行文件路径
    /// </summary>
    private void SelectMCPServerPath()
    {
        string extension = "";
        string title = "选择MCP服务器可执行文件";

        // 根据平台设置扩展名
        switch (Application.platform)
        {
            case RuntimePlatform.WindowsEditor:
                extension = "exe";
                break;
            case RuntimePlatform.OSXEditor:
            case RuntimePlatform.LinuxEditor:
                extension = "*";
                break;
            default:
                extension = "*";
                break;
        }

        string selectedPath = EditorUtility.OpenFilePanel(
            title,
            string.IsNullOrEmpty(mcpServerPath) ? Application.dataPath : System.IO.Path.GetDirectoryName(mcpServerPath),
            extension
        );

        if (!string.IsNullOrEmpty(selectedPath))
        {
            mcpServerPath = selectedPath;

            // 立即更新MCPServerManager
            if (mcpManager != null)
            {
                mcpManager.ExecutablePath = mcpServerPath;
            }

            AddLogMessage($"MCP服务器路径已更新: {selectedPath}");

            // 立即保存设置
            EditorPrefs.SetString(MCP_SERVER_PATH_PREF_KEY, mcpServerPath);
        }
    }

    // 事件处理
    private void OnClientConnected(System.Net.Sockets.TcpClient client)
    {
        // 确保在主线程更新UI
        EditorApplication.delayCall += () =>
        {
            UpdateClientCount();
            AddLogMessage($"新客户端已连接: {client?.Client?.RemoteEndPoint?.ToString() ?? "Unknown"}");
            Repaint();
        };
    }

    private void OnClientDisconnected(System.Net.Sockets.TcpClient client)
    {
        // 确保在主线程更新UI
        EditorApplication.delayCall += () =>
        {
            UpdateClientCount();
            AddLogMessage($"客户端已断开: {client?.Client?.RemoteEndPoint?.ToString() ?? "Unknown"}");
            Repaint();
        };
    }

    private void OnMCPStatusChanged(MCPServerManager.ServerStatus status)
    {
        // 确保在主线程更新UI
        EditorApplication.delayCall += () =>
        {
            isMcpServerRunning = (status == MCPServerManager.ServerStatus.Running);
            UpdateServerStatus();
            AddLogMessage($"MCP服务器状态变化: {status}");
            Repaint();
        };
    }

    private void OnMCPLogMessage(string message)
    {
        // 确保在主线程更新UI
        EditorApplication.delayCall += () =>
        {
            AddLogMessage($"[MCP] {message}");
        };
    }

    private void UpdateClientCount()
    {
        if (tcpServer != null)
        {
            connectedClients = tcpServer.GetConnectedClientCount();
        }
        else
        {
            connectedClients = 0;
        }
    }

    private void OnDestroy()
    {
        // 窗口关闭时保存设置
        SaveSettings();

        // 停止所有服务器
        if (isServerRunning)
        {
            StopServers();
        }

        // 清理资源
        if (mcpManager != null)
        {
            mcpManager.Dispose();
        }
    }
    
    public void Update()
    {
        UnityMCPMainThread.ExecuteMainThreadActions();
    }

    /// <summary>
    /// 获取工具分类信息
    /// </summary>
    private Dictionary<string, List<ToolInfo>> GetToolCategories()
    {
        var categories = new Dictionary<string, List<ToolInfo>>();

        // 基础工具分类
        categories["文件操作"] = new List<ToolInfo>
        {
            new ToolInfo("script_read", "读取Unity项目中的脚本文件内容"),
            new ToolInfo("script_write", "在Unity项目中创建或更新脚本文件")
        };

        categories["场景管理"] = new List<ToolInfo>
        {
            new ToolInfo("scene_get", "获取Unity当前场景层级数据"),
            new ToolInfo("scene_create_object", "在Unity场景中创建新的GameObject"),
            new ToolInfo("scene_object_add_component", "为场景中的GameObject添加组件"),
            new ToolInfo("scene_save", "保存当前或指定场景"),
            new ToolInfo("scene_load", "加载指定场景文件"),
            new ToolInfo("scene_get_info", "获取详细场景信息"),
            new ToolInfo("scene_find_objects", "在场景中按条件查找GameObject"),
            new ToolInfo("scene_delete_object", "删除场景中的GameObject")
        };

        categories["Transform操作"] = new List<ToolInfo>
        {
            new ToolInfo("scene_transform_get", "获取GameObject的Transform信息"),
            new ToolInfo("scene_transform_set", "设置GameObject的Transform信息")
        };

        categories["UI工具"] = new List<ToolInfo>
        {
            new ToolInfo("ui_rect_transform_set", "设置UI元素RectTransform属性（位置、大小、锚点）"),
            new ToolInfo("ui_rect_transform_get", "获取UI元素RectTransform信息"),
            new ToolInfo("ui_image_set", "设置UI Image组件属性（精灵、颜色、材质）"),
            new ToolInfo("ui_text_set", "设置UI Text组件属性（文本内容、字体、颜色）")
        };

        categories["资源管理"] = new List<ToolInfo>
        {
            new ToolInfo("asset_find", "按条件查找项目资源（路径、类型、名称）"),
            new ToolInfo("asset_get_info", "获取详细资源信息（元数据、导入设置）"),
            new ToolInfo("asset_get_dependencies", "获取资源依赖关系"),
            new ToolInfo("project_get_structure", "获取项目目录结构和统计信息")
        };

        categories["预制体操作"] = new List<ToolInfo>
        {
            new ToolInfo("prefab_create", "从场景GameObject创建预制体"),
            new ToolInfo("prefab_get_info", "获取详细预制体信息"),
            new ToolInfo("prefab_modify", "管理预制体实例修改")
        };

        categories["编辑器工具"] = new List<ToolInfo>
        {
            new ToolInfo("editor_get_logs", "读取Unity Editor Console日志")
        };

        return categories;
    }

    /// <summary>
    /// 导出工具清单
    /// </summary>
    private void ExportToolList()
    {
        try
        {
            var toolCategories = GetToolCategories();
            int totalTools = toolCategories.Values.Sum(list => list.Count);

            var exportText = new System.Text.StringBuilder();
            exportText.AppendLine("# Unity MCP 工具清单");
            exportText.AppendLine($"生成时间: {System.DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            exportText.AppendLine($"工具总数: {totalTools}");
            exportText.AppendLine($"分类数量: {toolCategories.Count}");
            exportText.AppendLine();

            foreach (var category in toolCategories)
            {
                exportText.AppendLine($"## {category.Key} ({category.Value.Count})");
                exportText.AppendLine();

                foreach (var tool in category.Value)
                {
                    exportText.AppendLine($"- **{tool.Name}**: {tool.Description}");
                }

                exportText.AppendLine();
            }

            // 保存到文件
            string fileName = $"UnityMCP_Tools_{System.DateTime.Now:yyyyMMdd_HHmmss}.md";
            string filePath = EditorUtility.SaveFilePanel("导出工具清单", "", fileName, "md");

            if (!string.IsNullOrEmpty(filePath))
            {
                System.IO.File.WriteAllText(filePath, exportText.ToString());
                AddLogMessage($"工具清单已导出到: {filePath}");
                
                // 在文件管理器中显示文件
                EditorUtility.RevealInFinder(filePath);
            }
        }
        catch (System.Exception e)
        {
            AddLogMessage($"导出工具清单失败: {e.Message}");
        }
    }

    /// <summary>
    /// 工具信息类
    /// </summary>
    public class ToolInfo
    {
        public string Name { get; set; }
        public string Description { get; set; }

        public ToolInfo(string name, string description)
        {
            Name = name;
            Description = description;
        }
    }
}