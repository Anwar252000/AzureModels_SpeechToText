import { useState, useEffect } from "react";

const Upload = () => {
  const [audioBlob, setAudioBlob] = useState(null);
  const [uploadedFile, setUploadedFile] = useState(null);
  const [response, setResponse] = useState([]);
  const [transcript, setTranscript] = useState(null);
  const [soap, setSoapNote] = useState(null);
  const [isUploading, setIsUploading] = useState(false);
  // const [isRecording, setIsRecording] = useState(false);
  // const mediaRecorderRef = useRef(null);
  // const audioChunksRef = useRef([]);

  // // Start recording
  // const startRecording = async () => {
  //   try {
  //     const stream = await navigator.mediaDevices.getUserMedia({ audio: true });
  //     const mediaRecorder = new MediaRecorder(stream);

  //     mediaRecorderRef.current = mediaRecorder;
  //     audioChunksRef.current = []; // Reset audio chunks

  //     mediaRecorder.ondataavailable = (event) => {
  //       if (event.data.size > 0) {
  //         audioChunksRef.current.push(event.data); // Collect chunks
  //       }
  //     };

  //     mediaRecorder.onstop = async () => {
  //       // Combine chunks into a single blob
  //       const audioBlob = new Blob(audioChunksRef.current, {
  //         type: "audio/webm",
  //       });
  //       setAudioBlob(audioBlob); // Update state with recorded audio
  //     };

  //     mediaRecorder.start();
  //     setIsRecording(true);
  //   } catch (error) {
  //     console.error("Error accessing microphone:", error);
  //   }
  // };

  // // Stop recording
  // const stopRecording = () => {
  //   if (mediaRecorderRef.current) {
  //     mediaRecorderRef.current.stop();
  //     setIsRecording(false);
  //   }
  // };

  // Handle file upload by the user
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
        "https://azuremodels20250110171746.azurewebsites.net/api/Speech/ExtractFromAudio",
        // "https://localhost:7152/api/Speech/ExtractFromAudio",
        {
          method: "POST",
          body: formData,
          headers: {
            "Ocp-Apim-Subscription-Key": "b82fbbf1-bb1e-457f-bc0c-dd7e0dee1eff",
          },
        }
      );

      if (!res.ok) {
        throw new Error(`Error: ${res.statusText}`);
      }

      const data = await res.json();

      // Transform response to table-friendly structure
      const transformedData = [];
      const entities = data.medicalEntities;
      const transcription = data.transcription;
      const soapNote = data.soapFormat;
      setTranscript(transcription);
      setSoapNote(soapNote);

      for (let i = 0; i < entities.length; i++) {
        if (entities[i].category === "MedicationName") {
          const dosage =
            entities[i - 1]?.category === "Dosage"
              ? entities[i - 1].text
              : "N/A";
          const frequency =
            entities[i + 1]?.category === "Frequency"
              ? entities[i + 1].text
              : "N/A";
          const time =
            entities[i + 2]?.category === "Time" ? entities[i + 2].text : "N/A";

          transformedData.push({
            MedicationName: entities[i].text,
            Dosage: dosage,
            Frequency: frequency,
            Time: time,
          });
        }
      }

      setResponse(transformedData); // Update state with transformed data
    } catch (error) {
      console.error("Error uploading audio:", error);
    }
    setIsUploading(false);
  };

  useEffect(() => {
    console.log(response, "Updated response data");
  }, [response]);

  // const renderTableData = () => {
  //   if (!response || response.length === 0) {
  //     return (
  //       <tr>
  //         <td colSpan="4">No data available</td>
  //       </tr>
  //     );
  //   }

  //   return response.map((row, index) => (
  //     <tr key={index}>
  //       <td style={{ padding: "0 20px 0 5px" }}>{row.MedicationName}</td>
  //       <td style={{ padding: "0 20px 0 5px" }}>{row.Dosage}</td>
  //       <td style={{ padding: "0 20px 0 5px" }}>{row.Frequency}</td>
  //       <td style={{ padding: "0 20px 0 5px" }}>{row.Time}</td>
  //     </tr>
  //   ));
  // };

  //   const generateSOAPNote = () => {
  //     if (!transcript || response.length === 0) {
  //       alert("No data available to generate SOAP note!");
  //       return;
  //     }

  //     const subjective = `Subjective:\n- Chief Complaint: ${transcript}\n`;
  //     const objective =
  //       "Objective:\n- Medications and observations as listed below.\n";
  //     const assessment =
  //       "Assessment:\n- See prescriptions for diagnosis and plan.\n";
  //     const plan = response
  //       .map(
  //         (item) =>
  //           `Plan:\n- Medication: ${item.MedicationName}, Dosage: ${item.Dosage}, Frequency: ${item.Frequency}, Time: ${item.Time}`
  //       )
  //       .join("\n");

  //     return `${subjective}\n${objective}\n${assessment}\n${plan}`;
  //   };

  //   const downloadSOAPNote = () => {
  //     const soapNote = generateSOAPNote();
  //     if (soapNote) {
  //       const blob = new Blob([soapNote], { type: "text/plain" });
  //       const url = URL.createObjectURL(blob);
  //       const a = document.createElement("a");
  //       a.href = url;
  //       a.download = "SOAP_Note.txt";
  //       a.click();
  //       URL.revokeObjectURL(url);
  //     }
  //   };

  return (
    <div className="App">
      <h2>Audio Uploader</h2>
      <div className="controls">
        {/* <h3>Record Audio</h3> */}
        {/* {!isRecording ? (
            <button onClick={startRecording}>Start Recording</button>
          ) : (
            <button onClick={stopRecording}>Stop Recording</button>
          )}
          {audioBlob && <p>Recorded audio ready for upload.</p>} */}

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
      {response && response.length > 0 ? (
        <div>
          <>
            <h4 style={{ textAlign: "left", padding: "0 0 0 320px" }}>
              Transcription
            </h4>
            <p style={{ textAlign: "left", padding: "0 320px 0 320px" }}>
              {transcript.split("\n").map((line, index) => (
                <p key={index}>{line}</p>
              ))}
            </p>
          </>
        </div>
      ) : (
        ""
      )}
      {response && response.length ? (
        <div>
          {(() => {
            // Split the SOAP Note into lines
            const lines = soap.split("\n");

            // Define an object to store parsed sections
            const soapSections = {
              Subjective: [],
              Objective: [],
              Assessment: [],
              Plan: [],
            };

            // Track the current section
            let currentSection = "";

            // Parse the lines to populate the soapSections object
            lines.forEach((line) => {
              const trimmedLine = line.trim();
              if (trimmedLine.endsWith(":")) {
                // Detect section headers
                currentSection = trimmedLine.replace(":", "");
              } else if (trimmedLine && currentSection) {
                // Add content to the current section
                soapSections[currentSection].push(trimmedLine);
              }
            });

            // Render the formatted output
            return (
              <div style={{ textAlign: "left", padding: "0 320px" }}>
                <p>
                  <strong>Subjective:</strong>{" "}
                  {soapSections.Subjective.join(", ")}
                </p>
                <p>
                  <strong>Objective:</strong>{" "}
                  {soapSections.Objective.join(", ")}
                </p>
                <p>
                  <strong>Assessment:</strong>{" "}
                  {soapSections.Assessment.join(", ")}
                </p>
                <p>
                  <strong>Plan:</strong> {soapSections.Plan.join(", ")}
                </p>
              </div>
            );
          })()}
        </div>
      ) : null}
    </div>
  );
};

export default Upload;

// {response && response.length > 0 ? (
//   <>
//     <h4 style={{ textAlign: "left", padding: "0 0 0 320px" }}>
//       Prescription
//     </h4>
//     <div className="response">
//       <table
//         className="table"
//         style={{
//           margin: "20px auto",
//           width: "100%",
//         }}
//       >
//         <thead>
//           <tr>
//             <th>Medication Name</th>
//             <th>Dosage</th>
//             <th>Frequency</th>
//             <th>Time</th>
//           </tr>
//         </thead>
//         <tbody>{renderTableData()}</tbody>
//       </table>
//       {/* <button onClick={downloadSOAPNote}>Download SOAP Note</button> */}
//     </div>
//   </>
// ) : (
//   <p>No data available.</p>
// )}
