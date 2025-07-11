using System.Collections.Generic;
using System.Net.Sockets;

/// <summary>
/// MCP工具接口，所有工具都需要实现此接口
/// </summary>
public interface IMCPTool
{
    /// <summary>
    /// 工具名称，对应action字段
    /// </summary>
    string ToolName { get; }
    
    /// <summary>
    /// 工具描述
    /// </summary>
    string Description { get; }
    
    /// <summary>
    /// 执行工具操作
    /// </summary>
    /// <param name="parameters">工具参数</param>
    /// <param name="client">发起请求的客户端</param>
    /// <returns>执行结果</returns>
    MCPResponse Execute(Dictionary<string, object> parameters, TcpClient client);
    
    /// <summary>
    /// 验证参数是否有效
    /// </summary>
    /// <param name="parameters">要验证的参数</param>
    /// <returns>验证结果，如果成功返回null，失败返回错误信息</returns>
    string ValidateParameters(Dictionary<string, object> parameters);
}
