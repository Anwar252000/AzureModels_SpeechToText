import { useState } from "react";

const WithSoap = () => {
  const [audioBlob, setAudioBlob] = useState(null);
  const [uploadedFile, setUploadedFile] = useState(null);
  const [response, setResponse] = useState([]);
  const [transcript, setTranscript] = useState(null);
  const [isUploading, setIsUploading] = useState(false);

  const handleFileUpload = (event) => {
    const file = event.target.files[0];
    if (file) {
      setUploadedFile(file);
      setAudioBlob(null); // Clear recorded audio if a file is uploaded
    }
  };

  // Upload audio to the backend
  const uploadAudio = async () => {
    if (!audioBlob && !uploadedFile) {
      alert("Please record or upload an audio file first!");
      return;
    }

    const formData = new FormData();

    // Append the appropriate audio source (recorded or uploaded)
    if (audioBlob) {
      formData.append(
        "audioFile",
        new File([audioBlob], "audio.webm", { type: "audio/webm" })
      );
    } else if (uploadedFile) {
      formData.append("audioFile", uploadedFile);
    }

    setIsUploading(true); // Start showing "Uploading..."

    try {
      const res = await fetch(
        "https://localhost:7152/api/Speech/ExtractFromAudio",
        {
          method: "POST",
          body: formData,
        }
      );

      if (!res.ok) {
        throw new Error(`Error: ${res.statusText}`);
      }

      const data = await res.json();
      const transcription = data.transcription;
      setTranscript(transcription);

      // Update the response state with the data including the entities
      setResponse(data.medicalEntities); // Set the medicalEntities from the response
    } catch (error) {
      console.error("Error uploading audio:", error);
    }
    setIsUploading(false);
  };

  const generateSOAP = () => {
    let subjective = [];
    let objective = [];
    let assessment = [];
    let plan = [];

    // Subjective: Extract symptoms and patient's description
    response.forEach((entity) => {
      if (entity.category === "SymptomOrSign") {
        subjective.push(entity.text); // Collect symptoms dynamically
      }
    });

    // Objective: Extract objective data (e.g., test results like cholesterol levels)
    response.forEach((entity) => {
      if (entity.category === "ExaminationName") {
        objective.push(`Test: ${entity.text}`);
      }
    });

    // Assessment: Extract any relevant diagnostic or condition assessment
    response.forEach((entity) => {
      if (entity.category === "HealthcareProfession") {
        assessment.push(`${entity.text}`);
      }

      if (
        entity.category === "SymptomOrSign" ||
        entity.category === "MeasurementValue"
      ) {
        assessment.push(`${entity.text}`);
      }
    });

    // Plan: Prescription and Treatment Plan (e.g., medications, follow-up)
    response.forEach((entity) => {
      if (entity.category === "MedicationName") {
        plan.push(
          `Prescribed ${entity.text} with dosage: ${
            entity.dosage || "Not specified"
          } for ${entity.frequency || "unspecified period"}`
        );
      }

      if (entity.category === "Dosage" || entity.category === "Frequency") {
        plan.push(`Dosage or Frequency: ${entity.text}`);
      }
    });

    return { subjective, objective, assessment, plan };
  };

  const soap = generateSOAP();

  return (
    <div className="App">
      <h2>Audio Uploader</h2>
      <div className="controls">
        <h3>Upload Pre-recorded Audio</h3>
        <input
          className="file-input"
          type="file"
          accept="audio/*"
          onChange={handleFileUpload}
        />
        {uploadedFile && <p>Uploaded file: {uploadedFile.name}</p>}

        <button onClick={uploadAudio} disabled={!audioBlob && !uploadedFile}>
          {isUploading ? "Processing..." : "Upload Audio"}
        </button>
      </div>

      {response && response.length > 0 && (
        <>
          <h4>Transcription</h4>
          <p>{transcript}</p>
        </>
      )}

      {response && response.length > 0 ? (
        <>
          <h4>SOAP Document</h4>
          <div className="soap-document">
            <h5>Subjective (S):</h5>
            <p>{soap.subjective.join(", ")}</p>

            <h5>Objective (O):</h5>
            <p>{soap.objective.join(", ")}</p>

            <h5>Assessment (A):</h5>
            <p>{soap.assessment.join(", ")}</p>

            <h5>Plan (P):</h5>
            <p>{soap.plan.join(", ")}</p>
          </div>
        </>
      ) : (
        <p>No data available.</p>
      )}
    </div>
  );
};

export default WithSoap;
