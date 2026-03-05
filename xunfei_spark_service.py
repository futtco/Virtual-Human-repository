#!/usr/bin/env python3
# coding: utf-8
"""
讯飞星火 AI 服务包装 - Flask HTTP 服务
支持多轮对话，供 Unity 客户端调用
"""

import os
import json
import logging
import threading
from typing import List, Dict, Optional
from flask import Flask, request, jsonify
import base64
import datetime
import hashlib
import hmac
from urllib.parse import urlparse
import ssl
from datetime import datetime as dt
from time import mktime
from urllib.parse import urlencode
from wsgiref.handlers import format_date_time
import websocket

# ============ 日志配置 ============
logging.basicConfig(
    level=logging.INFO,
    format='%(asctime)s - %(name)s - %(levelname)s - %(message)s'
)
logger = logging.getLogger(__name__)

app = Flask(__name__)

# ============ WebSocket 参数生成 ============
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


# ============ 对话会话管理 ============
class ChatSession:
    """单个用户的对话会话"""

    def __init__(self, session_id: str, appid: str, api_key: str, api_secret: str,
                 spark_url: str, domain: str, temperature: float = 0.5, max_tokens: int = 1024):
        self.session_id = session_id
        self.appid = appid
        self.api_key = api_key
        self.api_secret = api_secret
        self.spark_url = spark_url
        self.domain = domain
        self.temperature = temperature
        self.max_tokens = max_tokens

        # 对话历史
        self.messages: List[Dict[str, str]] = [
            {
                "role": "system",
                "content": "你是一个友好的虚拟人助手。使用中文回答用户问题。回复简洁，最多2-3句话，适合语音交互。"
            }
        ]

        # 最后的回复内容
        self.last_response = ""
        self.response_lock = threading.Lock()

    def add_user_message(self, content: str) -> None:
        """添加用户消息"""
        self.messages.append({"role": "user", "content": content})

        # 保持上下文大小（Spark Lite 限制 8192 tokens）
        self._trim_history()

    def add_assistant_message(self, content: str) -> None:
        """添加 AI 回复"""
        self.messages.append({"role": "assistant", "content": content})

    def _trim_history(self) -> None:
        """裁剪对话历史，保持在 token 限制内"""
        # 简单的启发式方法：如果消息太多，删除最早的用户-助手对
        while len(self.messages) > 10:  # 保留最多 10 条消息
            # 保留 system 消息
            if self.messages[1]["role"] == "user":
                del self.messages[1]
                if len(self.messages) > 1 and self.messages[1]["role"] == "assistant":
                    del self.messages[1]
            else:
                break

    def get_messages(self) -> List[Dict[str, str]]:
        """获取完整的消息历史"""
        return self.messages.copy()

    def chat(self, user_message: str) -> Optional[str]:
        """发送消息并获取回复"""
        self.add_user_message(user_message)
        logger.info(f"[Session {self.session_id}] User: {user_message}")

        try:
            response = self._ws_chat(self.get_messages())
            if response:
                self.add_assistant_message(response)
                logger.info(f"[Session {self.session_id}] Assistant: {response}")
                return response
            else:
                logger.error(f"[Session {self.session_id}] 获取回复失败")
                return None
        except Exception as e:
            logger.error(f"[Session {self.session_id}] 错误: {str(e)}")
            return None

    def _ws_chat(self, messages: List[Dict[str, str]]) -> Optional[str]:
        """通过 WebSocket 获取 AI 回复"""
        ws_param = WsParam(self.appid, self.api_key, self.api_secret, self.spark_url)
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

        def on_close(ws):
            logger.info("WebSocket 连接关闭")

        def on_open(ws):
            """WebSocket 连接建立后发送消息"""
            try:
                payload = {
                    "header": {
                        "app_id": self.appid,
                        "uid": self.session_id
                    },
                    "parameter": {
                        "chat": {
                            "domain": self.domain,
                            "temperature": self.temperature,
                            "max_tokens": self.max_tokens,
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
                logger.info(f"发送消息到 WebSocket: {self.domain}")
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


# ============ 会话管理 ============
sessions: Dict[str, ChatSession] = {}


def get_or_create_session(session_id: str) -> ChatSession:
    """获取或创建会话"""
    if session_id not in sessions:
        appid = os.getenv("XUNFEI_APP_ID", "")
        api_key = os.getenv("XUNFEI_API_KEY", "")
        api_secret = os.getenv("XUNFEI_API_SECRET", "")

        if not all([appid, api_key, api_secret]):
            raise ValueError("缺少讯飞凭证，请设置环境变量")

        sessions[session_id] = ChatSession(
            session_id=session_id,
            appid=appid,
            api_key=api_key,
            api_secret=api_secret,
            spark_url="wss://spark-api.xf-yun.com/v1.1/chat",  # Spark Lite
            domain="lite",
            temperature=0.5,
            max_tokens=1024
        )
        logger.info(f"创建新会话: {session_id}")

    return sessions[session_id]


# ============ Flask 路由 ============

@app.route('/health', methods=['GET'])
def health():
    """健康检查"""
    return jsonify({"status": "ok"}), 200


@app.route('/chat', methods=['POST'])
def chat():
    """
    聊天接口

    请求示例:
    {
        "message": "你好",
        "session_id": "user123"
    }

    响应示例:
    {
        "success": true,
        "response": "你好！很高兴认识你。有什么我可以帮助的吗？",
        "session_id": "user123"
    }
    """
    try:
        data = request.get_json() or {}
        message = data.get("message", "").strip()
        session_id = data.get("session_id", "default")

        if not message:
            return jsonify({
                "success": False,
                "response": None,
                "error": "消息不能为空",
                "session_id": session_id
            }), 400

        # 获取或创建会话
        session = get_or_create_session(session_id)

        # 获取 AI 回复
        response = session.chat(message)

        if response:
            return jsonify({
                "success": True,
                "response": response,
                "session_id": session_id,
                "error": None
            }), 200
        else:
            return jsonify({
                "success": False,
                "response": None,
                "error": "获取 AI 回复失败",
                "session_id": session_id
            }), 500

    except Exception as e:
        logger.error(f"聊天接口错误: {str(e)}")
        return jsonify({
            "success": False,
            "response": None,
            "error": str(e),
            "session_id": data.get("session_id", "default") if data else "unknown"
        }), 500


@app.route('/clear_history', methods=['POST'])
def clear_history():
    """
    清除对话历史

    请求示例:
    {
        "session_id": "user123"
    }
    """
    try:
        data = request.get_json() or {}
        session_id = data.get("session_id", "default")

        if session_id in sessions:
            # 重置消息历史，保留 system 消息
            sessions[session_id].messages = [
                {
                    "role": "system",
                    "content": "你是一个友好的虚拟人助手。使用中文回答用户问题。回复简洁，最多2-3句话，适合语音交互。"
                }
            ]
            logger.info(f"清除会话历史: {session_id}")

        return jsonify({
            "success": True,
            "message": f"历史已清除: {session_id}"
        }), 200

    except Exception as e:
        logger.error(f"清除历史错误: {str(e)}")
        return jsonify({
            "success": False,
            "error": str(e)
        }), 500


@app.route('/sessions', methods=['GET'])
def get_sessions():
    """获取所有会话列表（调试用）"""
    session_list = []
    for sid, session in sessions.items():
        session_list.append({
            "session_id": sid,
            "message_count": len(session.messages)
        })
    return jsonify({
        "sessions": session_list,
        "total": len(sessions)
    }), 200


if __name__ == '__main__':
    # 检查环境变量
    required_vars = ["XUNFEI_APP_ID", "XUNFEI_API_KEY", "XUNFEI_API_SECRET"]
    missing_vars = [var for var in required_vars if not os.getenv(var)]

    if missing_vars:
        logger.warning(f"缺少环境变量: {', '.join(missing_vars)}")
        logger.warning("请设置它们后再启动服务")

    logger.info("=" * 60)
    logger.info("讯飞星火 AI 服务启动")
    logger.info("=" * 60)
    logger.info(f"地址: http://127.0.0.1:5000")
    logger.info(f"模型: Spark Lite")
    logger.info("")
    logger.info("API 端点:")
    logger.info("  POST /chat - 发送消息")
    logger.info("  POST /clear_history - 清除历史")
    logger.info("  GET  /health - 健康检查")
    logger.info("  GET  /sessions - 查看会话列表（调试）")
    logger.info("=" * 60)

    app.run(host='127.0.0.1', port=5000, debug=False, threaded=True)
