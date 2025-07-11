using System;
using System.Collections.Generic;
using Newtonsoft.Json;

/// <summary>
/// MCP消息结构定义
/// </summary>
[Serializable]
public class MCPMessage
{
    [JsonProperty("action")]
    public string action;
    
    [JsonProperty("params")]
    public Dictionary<string, object> parameters;
    
    [JsonProperty("id")]
    public string id;
    
    [JsonProperty("timestamp")]
    public long timestamp;
    
    public MCPMessage()
    {
        parameters = new Dictionary<string, object>();
        id = Guid.NewGuid().ToString();
        timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    }
}

/// <summary>
/// MCP响应消息结构
/// </summary>
[Serializable]
public class MCPResponse
{
    [JsonProperty("success")]
    public bool success;
    
    [JsonProperty("data")]
    public object data;
    
    [JsonProperty("error")]
    public string error;
    
    [JsonProperty("id")]
    public string id;
    
    [JsonProperty("timestamp")]
    public long timestamp;
    
    public MCPResponse()
    {
        timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    }
    
    public static MCPResponse Success(object data, string id = null)
    {
        return new MCPResponse
        {
            success = true,
            data = data,
            id = id
        };
    }
    
    public static MCPResponse Error(string error, string id = null)
    {
        return new MCPResponse
        {
            success = false,
            error = error,
            id = id
        };
    }
}
