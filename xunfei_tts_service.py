"""
讯飞星火 + TTS 服务 (Python后端)

支持:
1. 聊天对话 (使用讯飞Spark API)
2. 文本转语音 (多个TTS后端选项)

安装依赖:
pip install flask requests gtts pydub numpy scipy

使用:
python xunfei_tts_service.py
"""

import os
import json
import base64
import io
import asyncio
import threading
import websocket
import ssl
import hashlib
import hmac
from urllib.parse import urlparse, urlencode
from wsgiref.handlers import format_date_time
from datetime import datetime as dt
from time import mktime
from typing import List, Dict, Optional
from flask import Flask, request, jsonify
from datetime import datetime
import logging

app = Flask(__name__)
logging.basicConfig(level=logging.INFO)
logger = logging.getLogger(__name__)

# ==================== TTS 实现 ====================

class TTSEngine:
    """TTS引擎接口"""
    def generate_speech(self, text: str) -> bytes:
        raise NotImplementedError

    def get_audio_format(self) -> str:
        """返回音频格式 (wav, mp3等)"""
        return "wav"  # 默认返回wav

class GTTSEngine(TTSEngine):
    """Google Text-to-Speech 引擎"""
    def __init__(self):
        from gtts import gTTS
        self.gTTS = gTTS
        logger.info("✓ gTTS 已加载")
    
    def get_audio_format(self) -> str:
        return "mp3"
    
    def generate_speech(self, text: str) -> bytes:
        try:
            logger.info(f"gTTS生成语音: {text[:50]}...")
            tts = self.gTTS(text=text, lang='zh-CN', slow=False)
            mp3_buffer = io.BytesIO()
            tts.write_to_fp(mp3_buffer)
            mp3_data = mp3_buffer.getvalue()
            logger.info(f"✓ 生成成功 (MP3, {len(mp3_data)} 字节)")
            return mp3_data
        except Exception as e:
            logger.error(f"gTTS生成失败: {e}")
            raise

class PyttsxEngine(TTSEngine):
    """pyttsx3 本地引擎 (离线)"""
    def __init__(self):
        try:
            import pyttsx3
            self.engine = pyttsx3.init()
            self.engine.setProperty('rate', 150)  # 语速
            self.engine.setProperty('volume', 0.9)  # 音量
            logger.info("✓ pyttsx3 已加载")
        except ImportError:
            logger.warning("⚠ pyttsx3 未安装，运行: pip install pyttsx3")
            self.engine = None

    def generate_speech(self, text: str) -> bytes:
        if self.engine is None:
            raise RuntimeError("pyttsx3 未安装")

        audio_io = io.BytesIO()
        self.engine.save_to_file(text, 'temp_audio.wav')
        self.engine.runAndWait()

        with open('temp_audio.wav', 'rb') as f:
            data = f.read()

        if os.path.exists('temp_audio.wav'):
            os.remove('temp_audio.wav')

        return data

class EdgeTTSEngine(TTSEngine):
    """Microsoft Edge TTS 引擎 - 支持长文本分段和重试"""
    def __init__(self):
        try:
            import edge_tts
            self.edge_tts = edge_tts
            logger.info("✓ Edge TTS 已加载")
            self.has_pydub = self._check_pydub()
            self.max_retries = 3  # 最大重试次数
            self.retry_delay = 2  # 重试延迟（秒）
        except ImportError as e:
            logger.warning(f"⚠ edge-tts 未安装: {e}")
            self.edge_tts = None
            self.has_pydub = False
    
    def get_audio_format(self) -> str:
        """返回实际的音频格式"""
        return "wav" if self.has_pydub else "mp3"
    
    def _check_pydub(self):
        """检查 pydub 是否可用"""
        try:
            __import__('pydub')
            logger.info("✓ pydub 已加载 (用于MP3->WAV转换)")
            return True
        except (ImportError, ModuleNotFoundError):
            logger.info("ℹ 使用 MP3 格式输出")
            return False
    
    def _split_text(self, text: str, max_length: int = 30) -> List[str]:
        """将长文本按标点符号分段"""
        segments = []
        current_segment = ""
        
        # 标点符号列表（包括中英文）
        delimiters = set('。！？，；：.!?,;')
        
        for char in text:
            current_segment += char
            
            # 遇到分隔符或达到最大长度
            if char in delimiters or len(current_segment) >= max_length:
                if current_segment.strip():
                    segments.append(current_segment.strip())
                current_segment = ""
        
        # 处理剩余文本
        if current_segment.strip():
            segments.append(current_segment.strip())
        
        return segments if segments else [text]
    
    async def _generate_single_with_retry(self, text: str, segment_index: int = 0, total_segments: int = 1) -> bytes:
        """生成单个文本段的语音，带重试机制"""
        last_error = None
        
        for attempt in range(self.max_retries):
            try:
                logger.info(f"生成语音 [{segment_index+1}/{total_segments}] 尝试 {attempt+1}/{self.max_retries}: {text[:30]}...")
                
                communicate = self.edge_tts.Communicate(
                    text=text,
                    voice="zh-CN-XiaoxuanNeural",
                    rate="+0%"
                )
                
                audio_data = io.BytesIO()
                chunk_count = 0
                
                async for chunk in communicate.stream():
                    if chunk["type"] == "audio":
                        audio_data.write(chunk["data"])
                        chunk_count += 1
                    elif chunk["type"] == "WordBoundary":
                        logger.debug(f"  收到文字边界: {chunk.get('text', '')}")
                
                result = audio_data.getvalue()
                logger.info(f"✓ 成功生成语音 [{segment_index+1}/{total_segments}] (大小: {len(result)} 字节, 音频块: {chunk_count})")
                return result
                
            except asyncio.TimeoutError as e:
                last_error = e
                logger.warning(f"✗ TTS超时 [{segment_index+1}/{total_segments}] (尝试 {attempt+1}/{self.max_retries}): {e}")
                
            except ConnectionError as e:
                last_error = e
                logger.warning(f"✗ TTS连接错误 [{segment_index+1}/{total_segments}] (尝试 {attempt+1}/{self.max_retries}): {e}")
                
            except Exception as e:
                last_error = e
                logger.warning(f"✗ TTS失败 [{segment_index+1}/{total_segments}] (尝试 {attempt+1}/{self.max_retries}): {type(e).__name__}: {e}")
            
            # 如果不是最后一次尝试，等待后重试
            if attempt < self.max_retries - 1:
                wait_time = self.retry_delay * (attempt + 1)  # 递增等待时间
                logger.info(f"  等待 {wait_time} 秒后重试...")
                await asyncio.sleep(wait_time)
        
        # 所有重试都失败
        raise RuntimeError(f"TTS生成失败 [{segment_index+1}/{total_segments}]，已重试 {self.max_retries} 次。最后错误: {last_error}")
    
    async def _generate_all_segments(self, segments: List[str]) -> bytes:
        """生成所有分段的语音，每个分段独立重试"""
        all_audio_data = b""
        failed_segments = []
        
        for i, segment in enumerate(segments):
            try:
                segment_audio = await self._generate_single_with_retry(
                    segment, 
                    segment_index=i, 
                    total_segments=len(segments)
                )
                all_audio_data += segment_audio
                
            except Exception as e:
                logger.error(f"✗ 分段 {i+1} 生成最终失败: {e}")
                failed_segments.append(i+1)
                # 继续处理下一段，不中断整体流程
        
        if failed_segments:
            logger.warning(f"⚠ 以下分段生成失败: {failed_segments}")
            
        if not all_audio_data:
            raise RuntimeError("所有分段生成失败，无可用音频数据")
        
        return all_audio_data
    
    def _run_async(self, coro):
        """运行异步协程的辅助方法"""
        try:
            loop = asyncio.get_event_loop()
            if loop.is_closed():
                loop = asyncio.new_event_loop()
                asyncio.set_event_loop(loop)
        except RuntimeError:
            loop = asyncio.new_event_loop()
            asyncio.set_event_loop(loop)
        return loop.run_until_complete(coro)
    
    def generate_speech(self, text: str) -> bytes:
        """生成语音（主入口，带完整重试机制）"""
        if self.edge_tts is None:
            raise RuntimeError("edge-tts 未安装")
        
        logger.info(f"开始TTS生成: 文本长度={len(text)} 字符")
        
        try:
            # 判断是否需要分段
            if len(text) > 30:
                logger.info(f"长文本检测，启用分段生成...")
                segments = self._split_text(text, max_length=30)
                logger.info(f"文本分为 {len(segments)} 段:")
                for i, seg in enumerate(segments):
                    logger.info(f"  段{i+1}: {seg[:30]}...")
                
                # 生成所有分段
                mp3_data = self._run_async(self._generate_all_segments(segments))
                logger.info(f"✓ 所有分段生成完成，总大小: {len(mp3_data)} 字节")
            else:
                # 短文本也使用重试机制
                logger.info(f"短文本，直接生成（带重试）...")
                async def generate_short():
                    return await self._generate_single_with_retry(text, segment_index=0, total_segments=1)
                
                mp3_data = self._run_async(generate_short())
                logger.info(f"✓ 短文本生成完成，大小: {len(mp3_data)} 字节")
            
            # 格式转换（如果需要）
            if self.has_pydub:
                from pydub import AudioSegment
                try:
                    logger.info("转换音频格式: MP3 → WAV")
                    audio = AudioSegment.from_mp3(io.BytesIO(mp3_data))
                    wav_buffer = io.BytesIO()
                    audio.export(wav_buffer, format="wav")
                    wav_data = wav_buffer.getvalue()
                    logger.info(f"✓ 格式转换完成，大小: {len(wav_data)} 字节")
                    return wav_data
                except Exception as e:
                    logger.warning(f"MP3转WAV失败，返回原始MP3: {e}")
                    return mp3_data
            else:
                return mp3_data
                
        except Exception as e:
            logger.error(f"✗ Edge TTS 生成最终失败: {e}")
            raise

class XunfeiTTSEngine(TTSEngine):
    """讯飞语音合成引擎"""
    def __init__(self):
        self.app_id = os.getenv('XUNFEI_APP_ID')
        self.api_key = os.getenv('XUNFEI_API_KEY')
        self.api_secret = os.getenv('XUNFEI_API_SECRET')

        if all([self.app_id, self.api_key, self.api_secret]):
            logger.info("✓ 讯飞TTS 已配置")
        else:
            logger.warning("⚠ 讯飞TTS 未配置 (缺少环境变量)")

    def generate_speech(self, text: str) -> bytes:
        # TODO: 实现讯飞TTS API调用
        # 需要查阅讯飞文档: https://www.xfyun.cn/doc/tts/online_tts/API
        raise NotImplementedError("讯飞TTS需要额外API实现")

# ==================== AI 实现 ====================

class AIEngine:
    """AI引擎接口"""
    def get_response(self, message: str, session_id: str) -> str:
        raise NotImplementedError

class WsParam:
    """WebSocket 连接参数生成"""
    def __init__(self, appid: str, api_key: str, api_secret: str, spark_url: str):
        self.appid = appid
        self.api_key = api_key
        self.api_secret = api_secret
        self.host = urlparse(spark_url).netloc
        self.path = urlparse(spark_url).path
        self.spark_url = spark_url

    def create_url(self) -> str:
        """生成带认证信息的 WebSocket URL"""
        now = dt.now()
        date = format_date_time(mktime(now.timetuple()))

        # 拼接签名字符串
        signature_origin = "host: " + self.host + "\n"
        signature_origin += "date: " + date + "\n"
        signature_origin += "GET " + self.path + " HTTP/1.1"

        # HMAC-SHA256 签名
        signature_sha = hmac.new(
            self.api_secret.encode('utf-8'),
            signature_origin.encode('utf-8'),
            digestmod=hashlib.sha256
        ).digest()

        signature_sha_base64 = base64.b64encode(signature_sha).decode(encoding='utf-8')

        authorization_origin = f'api_key="{self.api_key}", algorithm="hmac-sha256", headers="host date request-line", signature="{signature_sha_base64}"'
        authorization = base64.b64encode(authorization_origin.encode('utf-8')).decode(encoding='utf-8')

        v = {
            "authorization": authorization,
            "date": date,
            "host": self.host
        }

        url = self.spark_url + '?' + urlencode(v)
        return url

class XunfeiSparkEngine(AIEngine):
    """讯飞星火 AI 引擎 - WebSocket 实现"""
    def __init__(self):
        # 优先使用环境变量，其次使用默认凭证
        self.appid = os.getenv('XUNFEI_APP_ID', '015d7801')
        self.api_key = os.getenv('XUNFEI_API_KEY', 'd69d235ad78601c3b375024481b35572')
        self.api_secret = os.getenv('XUNFEI_API_SECRET', 'OTEzYWE5MGIyYzY4ZDIzY2M5OGI0NTg0')
        self.conversations = {}

        if all([self.appid, self.api_key, self.api_secret]):
            logger.info("✓ 讯飞星火 AI 已初始化")
        else:
            logger.warning("⚠ 讯飞凭证未完整设置")

    def get_response(self, message: str, session_id: str) -> str:
        if not all([self.appid, self.api_key, self.api_secret]):
            return "AI服务未初始化（缺少凭证）"

        # 初始化会话历史
        if session_id not in self.conversations:
            self.conversations[session_id] = [
                {
                    "role": "system",
                    "content": "你是一个友好的虚拟人助手。请遵守以下规则：1.使用中文回答 2.每次回复不超过25个字 3.回复要简洁明了，适合语音交互。"
                }
            ]

        # 添加用户消息
        self.conversations[session_id].append({
            "role": "user",
            "content": message
        })

        # 限制历史记录长度
        if len(self.conversations[session_id]) > 10:
            self.conversations[session_id] = self.conversations[session_id][:1] + self.conversations[session_id][-9:]

        try:
            response = self._ws_chat(self.conversations[session_id])

            if response:
                # 添加助手回复
                self.conversations[session_id].append({
                    "role": "assistant",
                    "content": response
                })
                return response
            else:
                return "AI服务响应失败"
        except Exception as e:
            logger.error(f"AI调用失败: {e}")
            return f"抱歉，AI服务出错: {str(e)}"

    def _ws_chat(self, messages: List[Dict[str, str]]) -> Optional[str]:
        """通过 WebSocket 获取 AI 回复"""
        spark_url = "wss://spark-api.xf-yun.com/x2"
        domain = "spark-x"

        logger.info(f"正在调用讯飞模型: domain={domain}, url={spark_url}")

        ws_param = WsParam(self.appid, self.api_key, self.api_secret, spark_url)
        ws_url = ws_param.create_url()

        response_text = ""
        response_event = threading.Event()

        def on_message(ws, message):
            nonlocal response_text
            try:
                data = json.loads(message)
                code = data.get('header', {}).get('code', -1)

                if code != 0:
                    logger.error(f"API 错误: {code}, {data}")
                    response_event.set()
                    return

                choices = data.get("payload", {}).get("choices", {})
                status = choices.get("status", -1)

                # 累积回复内容
                if "text" in choices and len(choices["text"]) > 0:
                    content = choices["text"][0].get("content", "")
                    response_text += content

                # status == 2 表示回复完成
                if status == 2:
                    response_event.set()
                    ws.close()
            except Exception as e:
                logger.error(f"解析消息失败: {str(e)}")
                response_event.set()

        def on_error(ws, error):
            logger.error(f"WebSocket 错误: {error}")
            response_event.set()

        def on_close(ws, close_status_code=None, close_msg=None):
            logger.debug("WebSocket 连接关闭")

        def on_open(ws):
            """WebSocket 连接建立后发送消息"""
            try:
                payload = {
                    "header": {
                        "app_id": self.appid,
                        "uid": "default"
                    },
                    "parameter": {
                        "chat": {
                            "domain": domain,
                            "temperature": 0.5,
                            "max_tokens": 500,
                            "auditing": "default"
                        }
                    },
                    "payload": {
                        "message": {
                            "text": messages
                        }
                    }
                }
                ws.send(json.dumps(payload))
                logger.debug(f"发送消息到 WebSocket")
            except Exception as e:
                logger.error(f"发送消息失败: {str(e)}")
                response_event.set()

        try:
            ws = websocket.WebSocketApp(
                ws_url,
                on_message=on_message,
                on_error=on_error,
                on_close=on_close,
                on_open=on_open
            )

            # 在线程中运行 WebSocket，避免阻塞
            ws_thread = threading.Thread(
                target=lambda: ws.run_forever(sslopt={"cert_reqs": ssl.CERT_NONE})
            )
            ws_thread.daemon = True
            ws_thread.start()

            # 等待回复（最多 30 秒）
            response_event.wait(timeout=30)

            return response_text if response_text else None
        except Exception as e:
            logger.error(f"WebSocket 连接失败: {str(e)}")
            return None

    def clear_history(self, session_id: str):
        if session_id in self.conversations:
            del self.conversations[session_id]
            logger.info(f"已清除会话 {session_id} 的历史")

# ==================== 全局实例 ====================


# 初始化TTS引擎 (使用 gTTS)
tts_engine = None
try:
    tts_engine = GTTSEngine()
    if tts_engine.gTTS is None:
        tts_engine = None
        logger.error("❌ gTTS 未安装，运行: pip install gtts")
    else:
        logger.info("✓ 使用 Google TTS 引擎")
except Exception as e:
    logger.error(f"❌ 初始化 gTTS 失败: {e}")

# 初始化AI引擎
ai_engine = XunfeiSparkEngine()

# ==================== API 端点 ====================

@app.route('/chat', methods=['POST'])
def chat():
    """
    聊天端点
    请求: {"message": "用户消息", "session_id": "会话ID"}
    响应: {"success": true, "response": "AI回复", "session_id": "会话ID"}
    """
    try:
        data = request.json
        message = data.get('message', '')
        session_id = data.get('session_id', 'default')

        if not message:
            return jsonify({
                'success': False,
                'error': '消息不能为空'
            }), 400

        logger.info(f"[{session_id}] 用户: {message}")

        response = ai_engine.get_response(message, session_id)
        logger.info(f"[{session_id}] 助手: {response}")

        return jsonify({
            'success': True,
            'response': response,
            'session_id': session_id
        })

    except Exception as e:
        logger.error(f"聊天处理失败: {e}")
        return jsonify({
            'success': False,
            'error': str(e)
        }), 500

@app.route('/tts', methods=['POST'])
def text_to_speech():
    """
    文本转语音端点
    请求: {"text": "要朗读的文本"}
    响应: {"success": true, "audio_data": "base64编码的音频", "format": "wav"}
    """
    try:
        # 详细请求日志
        logger.info(f"收到TTS请求 - Method: {request.method}, Content-Type: {request.content_type}")
        logger.info(f"请求头: {dict(request.headers)}")
        logger.info(f"原始数据: {request.get_data(as_text=True)}")

        data = request.json
        text = data.get('text', '') if data else ''
        logger.info(f"解析后的文本: {text}")

        if not text:
            logger.warning("文本为空")
            return jsonify({
                'success': False,
                'error': '文本不能为空'
            }), 400

        if tts_engine is None:
            return jsonify({
                'success': False,
                'error': '无可用的TTS引擎'
            }), 503

        logger.info(f"开始生成语音: {text}")
        audio_data = tts_engine.generate_speech(text)
        audio_format = tts_engine.get_audio_format()
        logger.info(f"语音生成成功，格式: {audio_format}, 长度: {len(audio_data)} 字节")

        # 转换为base64
        audio_base64 = base64.b64encode(audio_data).decode('utf-8')
        logger.info(f"Base64编码完成，长度: {len(audio_base64)} 字符")

        response = jsonify({
            'success': True,
            'audio_data': audio_base64,
            'format': audio_format
        })
        logger.info(f"返回TTS响应 (200 OK, 格式: {audio_format})")
        return response

    except Exception as e:
        logger.error(f"TTS处理失败: {e}")
        return jsonify({
            'success': False,
            'error': str(e)
        }), 500

@app.route('/clear_history', methods=['POST'])
def clear_history():
    """
    清除对话历史
    请求: {"session_id": "会话ID"}
    """
    try:
        data = request.json
        session_id = data.get('session_id', 'default')

        ai_engine.clear_history(session_id)

        return jsonify({
            'success': True,
            'message': f'已清除会话 {session_id} 的历史'
        })

    except Exception as e:
        logger.error(f"清除历史失败: {e}")
        return jsonify({
            'success': False,
            'error': str(e)
        }), 500

@app.route('/health', methods=['GET'])
def health():
    """健康检查端点"""
    logger.info("收到健康检查请求")
    return jsonify({
        'status': 'ok',
        'timestamp': datetime.now().isoformat(),
        'tts_engine': tts_engine.__class__.__name__ if tts_engine else 'None',
        'ai_engine': ai_engine.__class__.__name__
    })

@app.errorhandler(404)
def not_found(error):
    """捕获所有404错误，用于调试"""
    logger.warning(f"404 错误 - Path: {request.path}, Method: {request.method}")
    logger.warning(f"  User-Agent: {request.headers.get('User-Agent')}")
    logger.warning(f"  Request headers: {dict(request.headers)}")
    return jsonify({
        'success': False,
        'error': '路由未找到',
        'path': request.path,
        'method': request.method
    }), 404

# ==================== 启动 ====================

if __name__ == '__main__':
    print("""
    ╔═══════════════════════════════════════════════╗
    ║     讯飞星火 + TTS 综合服务                    ║
    ╚═══════════════════════════════════════════════╝

    📡 API 端点:
    - POST /chat           - 对话 AI
    - POST /tts            - 文本转语音
    - POST /clear_history  - 清除历史
    - GET  /health         - 健康检查

    🎯 启动地址: http://127.0.0.1:8000

    ⚙️  依赖检查:
    """)

    print(f"  AI 引擎: {ai_engine.__class__.__name__} ✓")
    print(f"  TTS 引擎: {tts_engine.__class__.__name__ if tts_engine else '❌ 未找到'}")

    print("\n📋 已注册的路由:")
    for rule in app.url_map.iter_rules():
        if rule.endpoint != 'static':
            print(f"  {rule.rule} -> {rule.methods}")

    print("\n💡 提示:")
    print("  1. 确保设置了环境变量: XUNFEI_APP_ID, XUNFEI_API_KEY, XUNFEI_API_SECRET")
    print("  2. 如果需要Google TTS, 运行: pip install gtts")
    print("  3. 或使用本地TTS, 运行: pip install pyttsx3")
    print("\n")

    logger.info("=" * 50)
    logger.info("服务启动完成，等待请求...")
    logger.info("=" * 50)

    app.run(host='127.0.0.1', port=8000, debug=False)
