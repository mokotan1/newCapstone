from fastapi import FastAPI, HTTPException
from pydantic import BaseModel
import os
import google.generativeai as genai
from groq import Groq
from dotenv import load_dotenv
import logging

# 로깅 설정
logging.basicConfig(level=logging.INFO)
logger = logging.getLogger(__name__)

load_dotenv()
app = FastAPI()

# --- 1. 환경 변수 설정 (Render Environment에서 설정한 이름과 일치해야 함) ---
# Tip: 보통 GROQ_API_KEY라는 이름을 더 많이 씁니다. Render 대시보드 이름을 확인하세요!
GROQ_API_KEY = os.getenv("capstone")  
GOOGLE_API_KEY = os.getenv("GOOGLE_API_KEY")

# Groq 초기화
groq_client = None
if GROQ_API_KEY:
    groq_client = Groq(api_key=GROQ_API_KEY)
    logger.info("✅ Groq 엔진 활성화됨")
else:
    logger.warning("⚠️ 'capstone' API 키를 찾을 수 없습니다.")

# Gemini 초기화
if GOOGLE_API_KEY:
    genai.configure(api_key=GOOGLE_API_KEY)
    logger.info("✅ Gemini 엔진 활성화됨 (Fallback)")

class ChatRequest(BaseModel):
    prompt: str
    system: str = "You are a helpful assistant."

@app.get("/")
def read_root():
    return {"status": "online", "message": "newCapstone AI Server is Running!"}

@app.post("/chat")
async def chat(request: ChatRequest):
    # 1순위: Groq (Llama 3.1)
    if groq_client:
        try:
            logger.info("🚀 Groq 호출 중...")
            completion = groq_client.chat.completions.create(
                # ❌ 기존 llama3-70b-8192는 서비스 종료됨
                # ✅ 최신 llama-3.1-8b-instant로 교체
                model="llama-3.1-8b-instant", 
                messages=[
                    {"role": "system", "content": request.system},
                    {"role": "user", "content": request.prompt}
                ],
                temperature=0.7,
                max_tokens=1024,
            )
            return {"response": completion.choices[0].message.content}
        except Exception as e:
            logger.error(f"❌ Groq 오류: {str(e)}")

    # 2순위: Gemini (Groq 실패 시 자동 전환)
    if GOOGLE_API_KEY:
        try:
            logger.info("🔄 Gemini로 전환 시도...")
            model = genai.GenerativeModel('gemini-1.5-flash-latest')
            # Gemini에 맞는 프롬프트 구성
            response = model.generate_content(f"System: {request.system}\n\nUser: {request.prompt}")
            return {"response": response.text}
        except Exception as e:
            logger.error(f"❌ Gemini 오류: {str(e)}")
            raise HTTPException(status_code=500, detail="모든 AI 엔진 작동 실패")

    raise HTTPException(status_code=500, detail="사용 가능한 API 키가 없습니다.")

if __name__ == "__main__":
    import uvicorn
    # Render는 PORT 환경 변수를 통해 포트를 지정합니다.
    port = int(os.environ.get("PORT", 8000))
    uvicorn.run(app, host="0.0.0.0", port=port)