from flask import Flask, request, jsonify
import requests
import json

app = Flask(__name__)

# Ollama 로컬 서버 주소 (기본값)
OLLAMA_URL = "http://localhost:11434/api/generate"

@app.route('/chat', methods=['POST'])
def chat():
    try:
        # 1. 유니티로부터 JSON 데이터 수신
        data = request.json
        user_prompt = data.get("prompt", "")
        system_prompt = data.get("system", "")

        # 2. Ollama에 보낼 페이로드 구성 (Llama 3 모델 사용) [cite: 18]
        # 시스템 프롬프트와 유저 입력을 하나로 합쳐서 전달합니다.
        combined_prompt = f"<|begin_of_text|><|start_header_id|>system<|end_header_id|>\n\n{system_prompt}<|eot_id|>"
        combined_prompt += f"<|start_header_id|>user<|end_header_id|>\n\n{user_prompt}<|eot_id|><|start_header_id|>assistant<|end_header_id|>\n\n"

        payload = {
            "model": "llama3",  # 또는 설치한 모델명(gemma 등) [cite: 18]
            "prompt": combined_prompt,
            "stream": False,    # 실시간 스트리밍 대신 한 번에 응답 받기
            "options": {
                "temperature": 0.7,
                "top_p": 0.9
            }
        }

        # 3. Ollama 서버로 요청 전송s
        response = requests.post(OLLAMA_URL, json=payload)
        response.raise_for_status()
        
        # 4. Ollama 응답 가공
        result = response.json()
        ai_response = result.get("response", "").strip()

        # 5. 유니티가 기대하는 형식 {"response": "..."}으로 반환
        return jsonify({"response": ai_response})

    except Exception as e:
        print(f"Error occurred: {str(e)}")
        print(f"--- AI의 답변 내용: {ai_response} ---")
        return jsonify({"response": ai_response})
        return jsonify({"response": f"서버 오류가 발생했습니다: {str(e)}"}), 500

if __name__ == '__main__':
    # 0.0.0.0으로 설정해야 같은 네트워크의 다른 기기에서도 접속 가능합니다.
    app.run(host='0.0.0.0', port=5000, debug=True)