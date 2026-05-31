document.addEventListener("DOMContentLoaded", () => {
    const connection = new signalR.HubConnectionBuilder()
        .withUrl("/chatHub")
        .withAutomaticReconnect()
        .build();

    const localVideo = document.getElementById("localVideo");
    const remoteVideo = document.getElementById("remoteVideo");
    const messages = document.getElementById("messages");
    const statusText = document.getElementById("statusText");
    const startBtn = document.getElementById("startBtn");
    const nextBtn = document.getElementById("nextBtn");
    const sendBtn = document.getElementById("sendBtn");
    const messageInput = document.getElementById("messageInput");

    let localStream = null;
    let peerConnection = null;
    let isInChat = false;

    const rtcConfig = {
        iceServers: [
            { urls: "stun:stun.l.google.com:19302" }
        ]
    };

    function setStatus(text) {
        if (statusText) {
            statusText.textContent = text;
        }
    }

    function appendMessage(sender, text) {
        const row = document.createElement("div");
        row.textContent = `${sender}: ${text}`;
        messages.appendChild(row);
        messages.scrollTop = messages.scrollHeight;
    }

    function clearMessages() {
        messages.innerHTML = "";
    }

    async function ensureLocalStream() {
        if (localStream) {
            return localStream;
        }

        localStream = await navigator.mediaDevices.getUserMedia({
            video: true,
            audio: true
        });

        localVideo.srcObject = localStream;
        return localStream;
    }

    function stopLocalStream() {
        if (!localStream) {
            return;
        }

        localStream.getTracks().forEach(track => track.stop());
        localStream = null;
        localVideo.srcObject = null;
    }

    function closePeerConnection() {
        if (!peerConnection) {
            return;
        }

        peerConnection.onicecandidate = null;
        peerConnection.ontrack = null;
        peerConnection.close();
        peerConnection = null;
        remoteVideo.srcObject = null;
    }

    function createPeerConnection() {
        closePeerConnection();

        peerConnection = new RTCPeerConnection(rtcConfig);

        localStream.getTracks().forEach(track => {
            peerConnection.addTrack(track, localStream);
        });

        peerConnection.onicecandidate = (event) => {
            if (!event.candidate) {
                return;
            }

            connection.invoke(
                "SendIceCandidate",
                JSON.stringify(event.candidate),
                event.candidate.sdpMid,
                event.candidate.sdpMLineIndex
            ).catch(console.error);
        };

        peerConnection.ontrack = (event) => {
            remoteVideo.srcObject = event.streams[0];
        };
    }

    async function startWebRtcAsInitiator() {
        createPeerConnection();

        const offer = await peerConnection.createOffer();
        await peerConnection.setLocalDescription(offer);
        await connection.invoke("SendOffer", offer.sdp);
    }

    async function handleOffer(sdp) {
        createPeerConnection();

        await peerConnection.setRemoteDescription({ type: "offer", sdp });
        const answer = await peerConnection.createAnswer();
        await peerConnection.setLocalDescription(answer);
        await connection.invoke("SendAnswer", answer.sdp);
    }

    async function handleAnswer(sdp) {
        if (!peerConnection) {
            return;
        }

        await peerConnection.setRemoteDescription({ type: "answer", sdp });
    }

    async function handleIceCandidate(candidateJson, sdpMid, sdpMLineIndex) {
        if (!peerConnection || !candidateJson) {
            return;
        }

        const candidate = JSON.parse(candidateJson);
        await peerConnection.addIceCandidate({
            candidate: candidate.candidate,
            sdpMid: sdpMid ?? candidate.sdpMid,
            sdpMLineIndex: sdpMLineIndex ?? candidate.sdpMLineIndex
        });
    }

    function resetChatUi() {
        isInChat = false;
        closePeerConnection();
        setStatus("Disconnected. Click Start Chat to find someone new.");
        startBtn.disabled = false;
        nextBtn.disabled = true;
        sendBtn.disabled = true;
        messageInput.disabled = true;
    }

    connection.on("WaitingForPartner", () => {
        setStatus("Looking for a stranger...");
        startBtn.disabled = true;
        nextBtn.disabled = false;
    });

    connection.on("Matched", async (isInitiator) => {
        isInChat = true;
        setStatus("Connected! Say hi.");
        startBtn.disabled = true;
        nextBtn.disabled = false;
        sendBtn.disabled = false;
        messageInput.disabled = false;
        clearMessages();
        appendMessage("System", "You are connected to a stranger.");

        try {
            if (isInitiator) {
                await startWebRtcAsInitiator();
            }
        } catch (err) {
            console.error(err);
            setStatus("Video connection failed. Try Next Stranger.");
        }
    });

    connection.on("ReceiveOffer", async (sdp) => {
        try {
            await handleOffer(sdp);
        } catch (err) {
            console.error(err);
        }
    });

    connection.on("ReceiveAnswer", async (sdp) => {
        try {
            await handleAnswer(sdp);
        } catch (err) {
            console.error(err);
        }
    });

    connection.on("ReceiveIceCandidate", async (candidate, sdpMid, sdpMLineIndex) => {
        try {
            await handleIceCandidate(candidate, sdpMid, sdpMLineIndex);
        } catch (err) {
            console.error(err);
        }
    });

    connection.on("ReceiveMessage", (message) => {
        appendMessage("Stranger", message);
    });

    connection.on("StrangerDisconnected", () => {
        appendMessage("System", "Stranger left.");
        resetChatUi();
        setStatus("Stranger disconnected. Click Start Chat to find someone new.");
    });

    connection.start()
        .then(() => setStatus("Ready. Click Start Chat."))
        .catch(err => {
            console.error(err);
            setStatus("Could not connect to server.");
        });

    startBtn.addEventListener("click", async () => {
        try {
            await ensureLocalStream();
            setStatus("Looking for a stranger...");
            startBtn.disabled = true;
            await connection.invoke("StartChat");
        } catch (err) {
            console.error(err);
            setStatus("Camera/microphone access is required.");
            startBtn.disabled = false;
        }
    });

    nextBtn.addEventListener("click", async () => {
        closePeerConnection();
        remoteVideo.srcObject = null;
        clearMessages();
        setStatus("Finding next stranger...");
        nextBtn.disabled = true;
        sendBtn.disabled = true;
        messageInput.disabled = true;
        isInChat = false;

        try {
            await connection.invoke("NextStranger");
        } catch (err) {
            console.error(err);
            setStatus("Could not find a new stranger.");
            nextBtn.disabled = false;
        }
    });

    sendBtn.addEventListener("click", sendChatMessage);

    messageInput.addEventListener("keydown", (event) => {
        if (event.key === "Enter") {
            sendChatMessage();
        }
    });

    function sendChatMessage() {
        const text = messageInput.value.trim();

        if (!text || !isInChat) {
            return;
        }

        connection.invoke("SendMessage", text)
            .then(() => {
                appendMessage("You", text);
                messageInput.value = "";
            })
            .catch(console.error);
    }

    window.addEventListener("beforeunload", () => {
        stopLocalStream();
        closePeerConnection();
    });
});
