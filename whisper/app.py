from flask import Flask, request, jsonify
import os
import whisper

app = Flask(__name__)

# Directory to save uploaded files
UPLOAD_FOLDER = 'uploads'
os.makedirs(UPLOAD_FOLDER, exist_ok=True)
app.config['UPLOAD_FOLDER'] = UPLOAD_FOLDER

# Load Whisper model
# Choose model size: tiny, base, small, medium, large
model = whisper.load_model("medium")


@app.route('/upload', methods=['POST'])
def upload_audio():
    if 'audio' not in request.files:
        return jsonify({"error": "No audio file provided"}), 400

    audio_file = request.files['audio']

    if audio_file.filename == '':
        return jsonify({"error": "No selected file"}), 400

    # Save file to uploads folder
    file_path = os.path.join(app.config['UPLOAD_FOLDER'], audio_file.filename)
    audio_file.save(file_path)

    try:
        # Transcribe audio using Whisper
        # Use task="translate" for translation
        result = model.transcribe(file_path, task="translate")
        return jsonify({"transcription": result['text']}), 200
    except Exception as e:
        return jsonify({"error": str(e)}), 500


if __name__ == '__main__':
    app.run(debug=True)
