using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using Whisper;

public class WhisperTest : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    public WhisperManager whisperManager;

    private AudioClip clip;
    private bool isRecording = false;

    public void StartRecording()
    {
        if(Microphone.devices.Length==0)
        {
            Debug.LogError("没有检测到麦克风设备!");
            return;
        }

        Debug.Log($"使用麦克风: {Microphone.devices[0]}");
        clip=Microphone.Start(null,false,5,16000);
        Debug.Log("开始录音... (最长5秒)");
    }

    public void StopRecording()
    {
        if(clip==null)
        {
            Debug.LogWarning("没有正在进行的录音!");
            return;
        }

        Microphone.End(null);
        Debug.Log($"录音结束,音频长度: {clip.length}秒, 采样数: {clip.samples}");

        float[] samples=new float[clip.samples];
        clip.GetData(samples,0);

        Debug.Log("开始识别...");
        Recognize(samples);
    }

    public async void Recognize(float[] samples)
    {
        if(whisperManager==null)
        {
            Debug.LogError("WhisperManager未设置!");
            return;
        }

        if(!whisperManager.IsLoaded)
        {
            Debug.LogWarning("Whisper模型未加载,等待加载完成...");
            await whisperManager.InitModel();
        }

        Debug.Log("正在识别语音...");
        var result=await whisperManager.GetTextAsync(samples,16000,1);

        if(result!=null && !string.IsNullOrEmpty(result.Result))
        {
            Debug.Log($"<color=green>识别结果: {result.Result}</color>");
        }
        else
        {
            Debug.LogWarning("未识别到语音内容");
        }
    }

    // 长按开始录音
    public void OnPointerDown(PointerEventData eventData)
    {
        if(isRecording) return;

        isRecording = true;
        StartRecording();
    }

    // 松手结束录音
    public void OnPointerUp(PointerEventData eventData)
    {
        if(!isRecording) return;

        isRecording = false;
        StopRecording();
    }
}
