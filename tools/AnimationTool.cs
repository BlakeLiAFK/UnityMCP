using System.Collections.Generic;
using System.Net.Sockets;
using UnityEngine;
using UnityEditor;

/// <summary>
/// 动画工具 - 播放和控制动画
/// </summary>
public class AnimationTool : IMCPTool
{
    public string ToolName => "animation_play";
    
    public string Description => "播放和控制GameObject上的动画";
    
    public MCPResponse Execute(Dictionary<string, object> parameters, TcpClient client)
    {
        try
        {
            // 获取必需参数
            if (!parameters.ContainsKey("instanceId"))
            {
                return MCPResponse.Error("缺少必需参数: instanceId");
            }
            
            if (!parameters.ContainsKey("animationName"))
            {
                return MCPResponse.Error("缺少必需参数: animationName");
            }
            
            int instanceId = System.Convert.ToInt32(parameters["instanceId"]);
            string animationName = parameters["animationName"].ToString();
            float speed = parameters.ContainsKey("speed") ? System.Convert.ToSingle(parameters["speed"]) : 1f;
            bool loop = parameters.ContainsKey("loop") ? System.Convert.ToBoolean(parameters["loop"]) : false;
            
            // 获取GameObject
            GameObject gameObject = EditorUtility.InstanceIDToObject(instanceId) as GameObject;
            if (gameObject == null)
            {
                return MCPResponse.Error($"未找到GameObject (InstanceID: {instanceId})");
            }
            
            // 尝试获取Animation组件
            Animation animationComponent = gameObject.GetComponent<Animation>();
            if (animationComponent != null)
            {
                return PlayLegacyAnimation(animationComponent, animationName, speed, loop, gameObject);
            }
            
            // 尝试获取Animator组件
            Animator animator = gameObject.GetComponent<Animator>();
            if (animator != null)
            {
                return PlayAnimatorAnimation(animator, animationName, speed, loop, gameObject);
            }
            
            return MCPResponse.Error($"GameObject '{gameObject.name}' 没有Animation或Animator组件");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"播放动画时出错: {e.Message}");
            return MCPResponse.Error($"播放动画失败: {e.Message}");
        }
    }
    
    /// <summary>
    /// 播放Legacy Animation系统的动画
    /// </summary>
    private MCPResponse PlayLegacyAnimation(Animation animationComponent, string animationName, float speed, bool loop, GameObject gameObject)
    {
        try
        {
            // 检查动画是否存在
            AnimationClip clip = animationComponent[animationName]?.clip;
            if (clip == null)
            {
                // 尝试通过名称查找
                foreach (AnimationState state in animationComponent)
                {
                    if (state.name == animationName)
                    {
                        clip = state.clip;
                        break;
                    }
                }
                
                if (clip == null)
                {
                    var availableAnimations = new List<string>();
                    foreach (AnimationState state in animationComponent)
                    {
                        availableAnimations.Add(state.name);
                    }
                    
                    return MCPResponse.Error($"动画 '{animationName}' 不存在。可用动画: {string.Join(", ", availableAnimations)}");
                }
            }
            
            // 设置动画状态
            AnimationState animationState = animationComponent[animationName];
            animationState.speed = speed;
            animationState.wrapMode = loop ? WrapMode.Loop : WrapMode.Once;
            
            // 播放动画
            animationComponent.Play(animationName);
            
            var result = new Dictionary<string, object>
            {
                ["gameObjectName"] = gameObject.name,
                ["instanceId"] = gameObject.GetInstanceID(),
                ["animationType"] = "Legacy",
                ["animationName"] = animationName,
                ["speed"] = speed,
                ["loop"] = loop,
                ["duration"] = clip.length,
                ["wrapMode"] = animationState.wrapMode.ToString(),
                ["isPlaying"] = animationComponent.IsPlaying(animationName)
            };
            
            Debug.Log($"成功播放Legacy动画: {gameObject.name} -> {animationName} (速度: {speed}, 循环: {loop})");
            
            return MCPResponse.Success(result);
        }
        catch (System.Exception e)
        {
            return MCPResponse.Error($"播放Legacy动画失败: {e.Message}");
        }
    }
    
    /// <summary>
    /// 播放Animator系统的动画
    /// </summary>
    private MCPResponse PlayAnimatorAnimation(Animator animator, string animationName, float speed, bool loop, GameObject gameObject)
    {
        try
        {
            // 设置速度
            animator.speed = speed;
            
            // 尝试播放动画状态
            bool hasState = false;
            int layerIndex = 0;
            
            // 检查所有层的状态
            for (int i = 0; i < animator.layerCount; i++)
            {
                if (animator.HasState(i, Animator.StringToHash(animationName)))
                {
                    hasState = true;
                    layerIndex = i;
                    break;
                }
            }
            
            if (!hasState)
            {
                // 尝试通过触发器播放
                var triggerNames = new List<string>();
                if (animator.runtimeAnimatorController != null)
                {
                    var animatorParams = animator.parameters;
                    foreach (var param in animatorParams)
                    {
                        if (param.type == AnimatorControllerParameterType.Trigger)
                        {
                            triggerNames.Add(param.name);
                            if (param.name == animationName)
                            {
                                animator.SetTrigger(animationName);
                                hasState = true;
                                break;
                            }
                        }
                    }
                }
                
                if (!hasState)
                {
                    return MCPResponse.Error($"Animator中没有找到状态或触发器 '{animationName}'。可用触发器: {string.Join(", ", triggerNames)}");
                }
            }
            else
            {
                // 直接播放状态
                animator.Play(animationName, layerIndex);
            }
            
            // 获取当前动画信息
            AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(layerIndex);
            
            var result = new Dictionary<string, object>
            {
                ["gameObjectName"] = gameObject.name,
                ["instanceId"] = gameObject.GetInstanceID(),
                ["animationType"] = "Animator",
                ["animationName"] = animationName,
                ["speed"] = speed,
                ["loop"] = loop,
                ["layerIndex"] = layerIndex,
                ["stateHash"] = stateInfo.shortNameHash,
                ["normalizedTime"] = stateInfo.normalizedTime,
                ["length"] = stateInfo.length
            };
            
            // 添加Animator Controller信息
            if (animator.runtimeAnimatorController != null)
            {
                result["controllerName"] = animator.runtimeAnimatorController.name;
                result["controllerPath"] = AssetDatabase.GetAssetPath(animator.runtimeAnimatorController);
            }
            
            // 添加参数信息
            var parameterList = new List<Dictionary<string, object>>();
            foreach (var param in animator.parameters)
            {
                parameterList.Add(new Dictionary<string, object>
                {
                    ["name"] = param.name,
                    ["type"] = param.type.ToString(),
                    ["defaultValue"] = GetParameterValue(animator, param)
                });
            }
            result["parameters"] = parameterList;
            
            Debug.Log($"成功播放Animator动画: {gameObject.name} -> {animationName} (速度: {speed})");
            
            return MCPResponse.Success(result);
        }
        catch (System.Exception e)
        {
            return MCPResponse.Error($"播放Animator动画失败: {e.Message}");
        }
    }
    
    /// <summary>
    /// 获取Animator参数值
    /// </summary>
    private object GetParameterValue(Animator animator, AnimatorControllerParameter param)
    {
        try
        {
            switch (param.type)
            {
                case AnimatorControllerParameterType.Bool:
                    return animator.GetBool(param.name);
                case AnimatorControllerParameterType.Float:
                    return animator.GetFloat(param.name);
                case AnimatorControllerParameterType.Int:
                    return animator.GetInteger(param.name);
                case AnimatorControllerParameterType.Trigger:
                    return "Trigger";
                default:
                    return null;
            }
        }
        catch
        {
            return null;
        }
    }
    
    public string ValidateParameters(Dictionary<string, object> parameters)
    {
        // 检查必需参数
        if (!parameters.ContainsKey("instanceId"))
        {
            return "缺少必需参数: instanceId";
        }
        
        if (!parameters.ContainsKey("animationName"))
        {
            return "缺少必需参数: animationName";
        }
        
        // 验证instanceId是否为有效数字
        if (!int.TryParse(parameters["instanceId"].ToString(), out _))
        {
            return "instanceId必须是有效的整数";
        }
        
        // 验证animationName不为空
        if (string.IsNullOrEmpty(parameters["animationName"].ToString()))
        {
            return "animationName不能为空";
        }
        
        // 验证speed参数（如果提供）
        if (parameters.ContainsKey("speed"))
        {
            if (!float.TryParse(parameters["speed"].ToString(), out _))
            {
                return "speed必须是有效的数字";
            }
        }
        
        return null;
    }
}