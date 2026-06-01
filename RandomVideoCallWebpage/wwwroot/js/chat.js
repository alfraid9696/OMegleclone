function countryFlag(code) {
    if (!code || code === "XX" || code.length !== 2) {
        return "🌐";
    }

    return code.toUpperCase().replace(/./g, (char) =>
        String.fromCodePoint(127397 + char.charCodeAt(0)));
}

document.addEventListener("DOMContentLoaded", () => {
    const chatPage = document.getElementById("chatPage");
    const localUserName = chatPage?.dataset.userName || "You";

    const connection = new signalR.HubConnectionBuilder()
        .withUrl("/chatHub")
        .withAutomaticReconnect()
        .build();

    const localVideo = document.getElementById("localVideo");
    const remoteVideo = document.getElementById("remoteVideo");
    const messages = document.getElementById("messages");
    const statusText = document.getElementById("statusText");
    const liveCountEl = document.getElementById("liveCount");
    const startBtn = document.getElementById("startBtn");
    const nextBtn = document.getElementById("nextBtn");
    const sendBtn = document.getElementById("sendBtn");
    const messageInput = document.getElementById("messageInput");
    const partnerNameEl = document.getElementById("partnerName");
    const partnerFlagEl = document.getElementById("partnerFlag");
    const partnerCountryEl = document.getElementById("partnerCountry");
    const localPingEl = document.getElementById("localPing");
    const partnerPingEl = document.getElementById("partnerPing");

    let localStream = null;
    let peerConnection = null;
    let pendingIceCandidates = [];
    let isInChat = false;
    let partnerName = "";
    let pingInterval = null;

    const rtcConfig = {
        iceServers: [{ urls: "stun:stun.l.google.com:19302" }]
    };

    function setStatus(text) {
        if (statusText) {
            statusText.textContent = text;
        }
    }

    function setLiveCount(count) {
        if (liveCountEl) {
            liveCountEl.textContent = count;
        }
    }

    function appendMessage(sender, text) {
        if (!messages) {
            return;
        }

        const row = document.createElement("div");
        row.className = "message-row";
        row.textContent = `${sender}: ${text}`;
        messages.appendChild(row);
        messages.scrollTop = messages.scrollHeight;
    }

    function clearMessages() {
        if (messages) {
            messages.innerHTML = "";
        }
    }

    function updatePartnerInfo(partner) {
        partnerName = partner?.name || "Guest";
        partnerNameEl.textContent = partnerName;
        partnerFlagEl.textContent = countryFlag(partner?.countryCode);
        partnerCountryEl.textContent = partner?.country || "";
    }

    function setPingVisible(show) {
        if (show) {
            localPingEl.removeAttribute("hidden");
            partnerPingEl.removeAttribute("hidden");
        } else {
            localPingEl.setAttribute("hidden", "");
            partnerPingEl.setAttribute("hidden", "");
            localPingEl.textContent = "";
            partnerPingEl.textContent = "";
        }
    }

    function resetPartnerInfo() {
        partnerName = "";
        partnerNameEl.textContent = "—";
        partnerFlagEl.textContent = "🌐";
        partnerCountryEl.textContent = "";
        setPingVisible(false);
    }

    function startPingLoop() {
        stopPingLoop();
        pingInterval = setInterval(() => {
            if (isInChat) {
                connection.invoke("SendPing", Date.now()).catch(console.error);
            }
        }, 2500);
    }

    function stopPingLoop() {
        if (pingInterval) {
            clearInterval(pingInterval);
            pingInterval = null;
        }
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

        localStream.getTracks().forEach((track) => track.stop());
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
        pendingIceCandidates = [];
        remoteVideo.srcObject = null;
    }

    function setRemoteVideoStream(stream) {
        remoteVideo.srcObject = stream;
        remoteVideo.play().catch(console.error);
    }

    async function flushPendingIceCandidates() {
        if (!peerConnection?.remoteDescription || pendingIceCandidates.length === 0) {
            return;
        }

        const queued = pendingIceCandidates;
        pendingIceCandidates = [];

        for (const candidateInit of queued) {
            try {
                await peerConnection.addIceCandidate(candidateInit);
            } catch (err) {
                console.error(err);
            }
        }
    }

    async function addIceCandidateSafe(candidateInit) {
        if (!peerConnection) {
            return;
        }

        if (!peerConnection.remoteDescription) {
            pendingIceCandidates.push(candidateInit);
            return;
        }

        await peerConnection.addIceCandidate(candidateInit);
    }

    function createPeerConnection() {
        closePeerConnection();

        peerConnection = new RTCPeerConnection(rtcConfig);

        localStream.getTracks().forEach((track) => {
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
            if (event.streams?.[0]) {
                setRemoteVideoStream(event.streams[0]);
                return;
            }

            if (event.track) {
                let stream = remoteVideo.srcObject;
                if (!(stream instanceof MediaStream)) {
                    stream = new MediaStream();
                    setRemoteVideoStream(stream);
                }

                stream.getTracks()
                    .filter((track) => track.kind === event.track.kind)
                    .forEach((track) => stream.removeTrack(track));
                stream.addTrack(event.track);
            }
        };
    }

    async function startWebRtcAsInitiator() {
        createPeerConnection();
        const offer = await peerConnection.createOffer();
        await peerConnection.setLocalDescription(offer);
        await connection.invoke("SendOffer", offer.sdp);
    }

    async function handleOffer(sdp) {
        await ensureLocalStream();
        createPeerConnection();
        await peerConnection.setRemoteDescription({ type: "offer", sdp });
        await flushPendingIceCandidates();
        const answer = await peerConnection.createAnswer();
        await peerConnection.setLocalDescription(answer);
        await connection.invoke("SendAnswer", answer.sdp);
    }

    async function handleAnswer(sdp) {
        if (!peerConnection) {
            return;
        }

        await peerConnection.setRemoteDescription({ type: "answer", sdp });
        await flushPendingIceCandidates();
    }

    async function handleIceCandidate(candidateJson, sdpMid, sdpMLineIndex) {
        if (!peerConnection || !candidateJson) {
            return;
        }

        const candidate = JSON.parse(candidateJson);
        await addIceCandidateSafe({
            candidate: candidate.candidate,
            sdpMid: sdpMid ?? candidate.sdpMid,
            sdpMLineIndex: sdpMLineIndex ?? candidate.sdpMLineIndex
        });
    }

    function resetChatUi() {
        isInChat = false;
        stopPingLoop();
        closePeerConnection();
        resetPartnerInfo();
        setStatus("Press Start to find someone.");
        startBtn.disabled = false;
        nextBtn.disabled = true;
        sendBtn.disabled = true;
        messageInput.disabled = true;
    }

    connection.on("LiveCountUpdated", (count) => setLiveCount(count));

    connection.on("AccountBlocked", () => {
        alert("Your account has been blocked.");
        window.location.href = "/Account/Logout?blocked=1";
    });

    connection.on("WaitingForPartner", () => {
        setStatus("Looking for someone to connect...");
        resetPartnerInfo();
        startBtn.disabled = true;
        nextBtn.disabled = false;
    });

    connection.on("Matched", async (partner, isInitiator) => {
        isInChat = true;
        updatePartnerInfo(partner);
        setPingVisible(true);
        setStatus(`Connected with ${partnerName}.`);
        startBtn.disabled = true;
        nextBtn.disabled = false;
        sendBtn.disabled = false;
        messageInput.disabled = false;
        clearMessages();
        appendMessage("System", `You are now connected with ${partnerName}.`);
        startPingLoop();

        try {
            await ensureLocalStream();

            if (isInitiator) {
                await startWebRtcAsInitiator();
            }
        } catch (err) {
            console.error(err);
            setStatus("Video connection failed. Try Next Person.");
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

    connection.on("ReceiveMessage", (senderName, message) => {
        appendMessage(senderName, message);
    });

    connection.on("PartnerDisconnected", () => {
        const name = partnerName || "Your partner";
        appendMessage("System", `${name} left the chat.`);
        resetChatUi();
        setStatus(`${name} disconnected.`);
    });

    connection.onreconnecting(() => {
        closePeerConnection();
        isInChat = false;
        stopPingLoop();
        sendBtn.disabled = true;
        messageInput.disabled = true;
        setStatus("Reconnecting...");
    });

    connection.onreconnected(() => {
        resetChatUi();
        setStatus("Reconnected. Press Start to find someone.");
    });

    connection.onclose(() => {
        resetChatUi();
        setStatus("Connection lost. Refresh the page.");
    });

    connection.on("PartnerPing", (sentAt) => {
        const ms = Math.max(0, Date.now() - sentAt);
        partnerPingEl.textContent = `${ms} ms`;
        connection.invoke("ReplyPing", sentAt).catch(console.error);
    });

    connection.on("PartnerPong", (sentAt) => {
        const ms = Math.max(0, Date.now() - sentAt);
        localPingEl.textContent = `${ms} ms`;
    });

    connection.start()
        .then(() => setStatus("Press Start when you are ready."))
        .catch((err) => {
            console.error(err);
            setStatus("Could not connect to server.");
        });

    startBtn.addEventListener("click", async () => {
        try {
            await ensureLocalStream();
            setStatus("Looking for someone to connect...");
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
        stopPingLoop();
        setStatus("Finding next person...");
        nextBtn.disabled = true;
        sendBtn.disabled = true;
        messageInput.disabled = true;
        isInChat = false;

        try {
            await connection.invoke("NextStranger");
        } catch (err) {
            console.error(err);
            setStatus("Could not find a new person.");
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
            .then((delivered) => {
                if (delivered) {
                    appendMessage(localUserName, text);
                    messageInput.value = "";
                } else {
                    appendMessage("System", "Message could not be delivered. Try Next Person.");
                }
            })
            .catch((err) => {
                console.error(err);
                appendMessage("System", "Failed to send message. Check your connection.");
            });
    }

    window.addEventListener("beforeunload", () => {
        stopPingLoop();
        stopLocalStream();
        closePeerConnection();
    });
});
