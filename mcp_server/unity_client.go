package main

import (
	"encoding/binary"
	"encoding/json"
	"errors"
	"fmt"
	"net"
	"time"
)

// debugMode在main.go中定义

// UnityTCPClient Unity TCP客户端
type UnityTCPClient struct {
	host    string
	port    string
	conn    net.Conn
	timeout time.Duration
}

// NewUnityTCPClient 创建新的Unity TCP客户端
func NewUnityTCPClient(host, port string) *UnityTCPClient {
	return &UnityTCPClient{
		host:    host,
		port:    port,
		timeout: 10 * time.Second,
	}
}

// Connect 连接到Unity服务器
func (c *UnityTCPClient) Connect() error {
	connectStart := time.Now()
	addr := fmt.Sprintf("%s:%s", c.host, c.port)
	
	if debugMode {
		fmt.Printf("[DEBUG] === TCP CONNECTION START ===\n")
		fmt.Printf("[DEBUG] Target address: %s\n", addr)
		fmt.Printf("[DEBUG] Connection timeout: %v\n", c.timeout)
		fmt.Printf("[DEBUG] Connection attempt start time: %s\n", connectStart.Format("15:04:05.000"))
	}

	conn, err := net.DialTimeout("tcp", addr, c.timeout)
	connectDuration := time.Since(connectStart)
	
	if err != nil {
		if debugMode {
			fmt.Printf("[DEBUG] === TCP CONNECTION FAILED ===\n")
			fmt.Printf("[DEBUG] Target: %s\n", addr)
			fmt.Printf("[DEBUG] Connect duration: %v\n", connectDuration)
			fmt.Printf("[DEBUG] Error type: %T\n", err)
			fmt.Printf("[DEBUG] Error details: %v\n", err)
		}
		return fmt.Errorf("failed to connect to Unity server %s: %w", addr, err)
	}

	c.conn = conn
	
	if debugMode {
		fmt.Printf("[DEBUG] === TCP CONNECTION SUCCESS ===\n")
		fmt.Printf("[DEBUG] Target: %s\n", addr)
		fmt.Printf("[DEBUG] Connect duration: %v\n", connectDuration)
		fmt.Printf("[DEBUG] Local address: %s\n", conn.LocalAddr())
		fmt.Printf("[DEBUG] Remote address: %s\n", conn.RemoteAddr())
		fmt.Printf("[DEBUG] Connection type: %s\n", conn.RemoteAddr().Network())
	}
	
	fmt.Printf("✓ Successfully connected to Unity server %s (took %v)\n", addr, connectDuration)
	return nil
}

// Close 关闭连接
func (c *UnityTCPClient) Close() error {
	if c.conn != nil {
		if debugMode {
			fmt.Printf("[DEBUG] === TCP CONNECTION CLOSE ===\n")
			fmt.Printf("[DEBUG] Closing connection to: %s\n", c.conn.RemoteAddr())
			fmt.Printf("[DEBUG] Local address: %s\n", c.conn.LocalAddr())
		}
		
		err := c.conn.Close()
		c.conn = nil
		
		if debugMode {
			if err != nil {
				fmt.Printf("[DEBUG] Connection close error: %v\n", err)
			} else {
				fmt.Printf("[DEBUG] Connection closed successfully\n")
			}
		}
		
		return err
	} else {
		if debugMode {
			fmt.Printf("[DEBUG] Close() called but connection is already nil\n")
		}
	}
	return nil
}

// SendMessage 发送消息到Unity并接收响应
func (c *UnityTCPClient) SendMessage(message map[string]interface{}) (map[string]interface{}, error) {
	sendStart := time.Now()
	
	// 确保连接存在
	if c.conn == nil {
		if debugMode {
			fmt.Printf("[DEBUG] No existing connection, establishing new connection\n")
		}
		if err := c.Connect(); err != nil {
			return nil, err
		}
	}

	// 序列化消息
	jsonData, err := json.Marshal(message)
	if err != nil {
		if debugMode {
			fmt.Printf("[DEBUG] JSON serialization failed: %v\n", err)
		}
		return nil, fmt.Errorf("failed to serialize message: %w", err)
	}

	messageId := ""
	if id, exists := message["id"]; exists {
		messageId = fmt.Sprintf("%v", id)
	}

	if debugMode {
		fmt.Printf("[DEBUG] === TCP SEND START === (ID: %s)\n", messageId)
		fmt.Printf("[DEBUG] Message size: %d bytes\n", len(jsonData))
		fmt.Printf("→ Sending to Unity: %s\n", string(jsonData))
	}

	// 创建4字节长度头（大端序）
	messageLen := uint32(len(jsonData))
	lengthHeader := make([]byte, 4)
	binary.BigEndian.PutUint32(lengthHeader, messageLen)

	if debugMode {
		fmt.Printf("[DEBUG] Message length header: %d bytes\n", messageLen)
	}

	// 设置写入超时
	writeDeadline := time.Now().Add(c.timeout)
	if err := c.conn.SetWriteDeadline(writeDeadline); err != nil {
		if debugMode {
			fmt.Printf("[DEBUG] Failed to set write deadline: %v\n", err)
		}
		return nil, fmt.Errorf("failed to set write deadline: %w", err)
	}

	// 发送长度头
	headerStart := time.Now()
	if _, err := c.conn.Write(lengthHeader); err != nil {
		if debugMode {
			fmt.Printf("[DEBUG] Failed to send header after %v: %v\n", time.Since(headerStart), err)
		}
		c.reconnect()
		return nil, fmt.Errorf("failed to send message header: %w", err)
	}
	
	if debugMode {
		fmt.Printf("[DEBUG] Header sent successfully in %v\n", time.Since(headerStart))
	}

	// 发送消息体
	bodyStart := time.Now()
	if _, err := c.conn.Write(jsonData); err != nil {
		if debugMode {
			fmt.Printf("[DEBUG] Failed to send body after %v: %v\n", time.Since(bodyStart), err)
		}
		c.reconnect()
		return nil, fmt.Errorf("failed to send message body: %w", err)
	}
	
	if debugMode {
		fmt.Printf("[DEBUG] Body sent successfully in %v\n", time.Since(bodyStart))
		fmt.Printf("[DEBUG] Total send time: %v\n", time.Since(sendStart))
	}

	// 接收响应
	if debugMode {
		fmt.Printf("[DEBUG] === TCP RECEIVE START === (ID: %s)\n", messageId)
	}
	
	response, err := c.receiveMessage()
	if err != nil {
		if debugMode {
			fmt.Printf("[DEBUG] Failed to receive response: %v\n", err)
		}
		c.reconnect()
		return nil, fmt.Errorf("failed to receive response: %w", err)
	}

	totalTime := time.Since(sendStart)
	if debugMode {
		fmt.Printf("[DEBUG] === TCP COMPLETE === (ID: %s, Total: %v)\n", messageId, totalTime)
	}

	return response, nil
}

// receiveMessage 接收Unity响应消息
func (c *UnityTCPClient) receiveMessage() (map[string]interface{}, error) {
	receiveStart := time.Now()
	
	// 设置读取超时
	readDeadline := time.Now().Add(c.timeout)
	if err := c.conn.SetReadDeadline(readDeadline); err != nil {
		if debugMode {
			fmt.Printf("[DEBUG] Failed to set read deadline: %v\n", err)
		}
		return nil, fmt.Errorf("failed to set read deadline: %w", err)
	}

	// 读取4字节长度头
	headerStart := time.Now()
	lengthHeader := make([]byte, 4)
	if _, err := c.conn.Read(lengthHeader); err != nil {
		if debugMode {
			fmt.Printf("[DEBUG] Failed to read header after %v: %v\n", time.Since(headerStart), err)
		}
		return nil, fmt.Errorf("failed to read message header: %w", err)
	}

	if debugMode {
		fmt.Printf("[DEBUG] Header received in %v\n", time.Since(headerStart))
	}

	// 解析消息长度
	messageLen := binary.BigEndian.Uint32(lengthHeader)
	if messageLen == 0 {
		if debugMode {
			fmt.Printf("[DEBUG] Received empty message (length=0)\n")
		}
		return nil, errors.New("received empty message")
	}

	if messageLen > 1024*1024 { // 限制消息大小为1MB
		if debugMode {
			fmt.Printf("[DEBUG] Message too large: %d bytes (max 1MB)\n", messageLen)
		}
		return nil, fmt.Errorf("message too large: %d bytes", messageLen)
	}

	if debugMode {
		fmt.Printf("← Response length: %d bytes\n", messageLen)
	}

	// 读取消息体
	bodyStart := time.Now()
	messageData := make([]byte, messageLen)
	totalRead := 0
	for totalRead < int(messageLen) {
		n, err := c.conn.Read(messageData[totalRead:])
		if err != nil {
			if debugMode {
				fmt.Printf("[DEBUG] Failed to read body at %d/%d bytes after %v: %v\n", 
					totalRead, messageLen, time.Since(bodyStart), err)
			}
			return nil, fmt.Errorf("failed to read message body: %w", err)
		}
		totalRead += n
		
		if debugMode && totalRead > 0 {
			fmt.Printf("[DEBUG] Read %d/%d bytes (%d%% complete)\n", 
				totalRead, messageLen, (totalRead*100)/int(messageLen))
		}
	}

	if debugMode {
		fmt.Printf("[DEBUG] Body received in %v\n", time.Since(bodyStart))
		fmt.Printf("← Received Unity response: %s\n", string(messageData))
	}

	// 解析JSON响应
	parseStart := time.Now()
	var response map[string]interface{}
	if err := json.Unmarshal(messageData, &response); err != nil {
		if debugMode {
			fmt.Printf("[DEBUG] JSON parsing failed after %v: %v\n", time.Since(parseStart), err)
			fmt.Printf("[DEBUG] Raw response data: %s\n", string(messageData))
		}
		return nil, fmt.Errorf("failed to parse JSON response: %w", err)
	}

	if debugMode {
		fmt.Printf("[DEBUG] JSON parsed in %v\n", time.Since(parseStart))
		fmt.Printf("[DEBUG] Total receive time: %v\n", time.Since(receiveStart))
	}

	return response, nil
}

// reconnect 重新连接到Unity服务器
func (c *UnityTCPClient) reconnect() {
	reconnectStart := time.Now()
	
	if debugMode {
		fmt.Printf("[DEBUG] === TCP RECONNECTION START ===\n")
		fmt.Printf("[DEBUG] Reconnection triggered at: %s\n", reconnectStart.Format("15:04:05.000"))
		fmt.Printf("[DEBUG] Target server: %s:%s\n", c.host, c.port)
	}
	
	fmt.Println("⚠ Connection lost detected, attempting to reconnect...")
	
	// 关闭现有连接
	closeStart := time.Now()
	c.Close()
	closeDuration := time.Since(closeStart)
	
	if debugMode {
		fmt.Printf("[DEBUG] Existing connection closed in %v\n", closeDuration)
		fmt.Printf("[DEBUG] Waiting 1 second before reconnection attempt...\n")
	}

	// 等待1秒后重试
	time.Sleep(time.Second)

	// 重连尝试
	connectStart := time.Now()
	if err := c.Connect(); err != nil {
		connectDuration := time.Since(connectStart)
		totalDuration := time.Since(reconnectStart)
		
		fmt.Printf("✗ Reconnection failed: %v\n", err)
		if debugMode {
			fmt.Printf("[DEBUG] === TCP RECONNECTION FAILED ===\n")
			fmt.Printf("[DEBUG] Connect attempt duration: %v\n", connectDuration)
			fmt.Printf("[DEBUG] Total reconnection duration: %v\n", totalDuration)
			fmt.Printf("[DEBUG] Error: %v\n", err)
		}
	} else {
		connectDuration := time.Since(connectStart)
		totalDuration := time.Since(reconnectStart)
		
		fmt.Printf("✓ Successfully reconnected to Unity server\n")
		if debugMode {
			fmt.Printf("[DEBUG] === TCP RECONNECTION SUCCESS ===\n")
			fmt.Printf("[DEBUG] Connect duration: %v\n", connectDuration)
			fmt.Printf("[DEBUG] Total reconnection duration: %v\n", totalDuration)
		}
	}
}

// IsConnected 检查是否已连接
func (c *UnityTCPClient) IsConnected() bool {
	checkStart := time.Now()
	
	if c.conn == nil {
		if debugMode {
			fmt.Printf("[DEBUG] IsConnected: connection is nil\n")
		}
		return false
	}

	if debugMode {
		fmt.Printf("[DEBUG] === CONNECTION CHECK START ===\n")
		fmt.Printf("[DEBUG] Remote address: %s\n", c.conn.RemoteAddr())
		fmt.Printf("[DEBUG] Performing write test to check connection status...\n")
	}

	// 尝试写入一个空的测试包来检测连接状态
	c.conn.SetWriteDeadline(time.Now().Add(time.Second))
	_, err := c.conn.Write([]byte{})
	checkDuration := time.Since(checkStart)
	
	if err != nil {
		if debugMode {
			fmt.Printf("[DEBUG] === CONNECTION CHECK FAILED ===\n")
			fmt.Printf("[DEBUG] Check duration: %v\n", checkDuration)
			fmt.Printf("[DEBUG] Write test error: %v\n", err)
		}
		return false
	}

	if debugMode {
		fmt.Printf("[DEBUG] === CONNECTION CHECK SUCCESS ===\n")
		fmt.Printf("[DEBUG] Check duration: %v\n", checkDuration)
		fmt.Printf("[DEBUG] Connection is alive\n")
	}

	return true
}

// TestConnection 测试与Unity的连接
func (c *UnityTCPClient) TestConnection() error {
	testStart := time.Now()
	testId := fmt.Sprintf("test_connection_%d", time.Now().UnixNano())
	
	if debugMode {
		fmt.Printf("[DEBUG] === CONNECTION TEST START ===\n")
		fmt.Printf("[DEBUG] Test ID: %s\n", testId)
		fmt.Printf("[DEBUG] Test start time: %s\n", testStart.Format("15:04:05.000"))
	}
	
	testMessage := map[string]interface{}{
		"action":    "ping",
		"params":    map[string]interface{}{},
		"id":        testId,
		"timestamp": time.Now().UnixMilli(),
	}

	if debugMode {
		fmt.Printf("[DEBUG] Test message: %s\n", formatJSON(testMessage))
	}

	response, err := c.SendMessage(testMessage)
	testDuration := time.Since(testStart)
	
	if err != nil {
		if debugMode {
			fmt.Printf("[DEBUG] === CONNECTION TEST FAILED ===\n")
			fmt.Printf("[DEBUG] Test duration: %v\n", testDuration)
			fmt.Printf("[DEBUG] Send message error: %v\n", err)
		}
		return err
	}

	if debugMode {
		fmt.Printf("[DEBUG] Test response received: %s\n", formatJSON(response))
	}

	if success, ok := response["success"].(bool); !ok || !success {
		errorMsg := "unknown error"
		if errStr, ok := response["error"].(string); ok {
			errorMsg = errStr
		}
		
		if debugMode {
			fmt.Printf("[DEBUG] === CONNECTION TEST FAILED ===\n")
			fmt.Printf("[DEBUG] Test duration: %v\n", testDuration)
			fmt.Printf("[DEBUG] Success field validation failed\n")
			fmt.Printf("[DEBUG] Success value: %v (type: %T)\n", response["success"], response["success"])
			fmt.Printf("[DEBUG] Error message: %s\n", errorMsg)
		}
		
		return fmt.Errorf("unity connection test failed: %s", errorMsg)
	}

	if debugMode {
		fmt.Printf("[DEBUG] === CONNECTION TEST SUCCESS ===\n")
		fmt.Printf("[DEBUG] Test duration: %v\n", testDuration)
		fmt.Printf("[DEBUG] Response validation passed\n")
	}

	fmt.Printf("✓ Unity connection test successful (took %v)\n", testDuration)
	return nil
}
