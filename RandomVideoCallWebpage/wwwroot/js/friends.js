document.addEventListener("DOMContentLoaded", () => {
    const chatPage = document.getElementById("chatPage");
    if (!chatPage) {
        return;
    }

    const currentUserId = chatPage.dataset.userId || "";
    const friendsPanel = document.getElementById("friendsPanel");
    const friendsBackdrop = document.getElementById("friendsBackdrop");
    const friendsTabContent = document.getElementById("friendsTabContent");
    const friendChatView = document.getElementById("friendChatView");
    const friendChatMessages = document.getElementById("friendChatMessages");
    const friendChatTitle = document.getElementById("friendChatTitle");
    const friendChatOnline = document.getElementById("friendChatOnline");
    const friendChatInput = document.getElementById("friendChatInput");
    const navFriendBadge = document.getElementById("navFriendBadge");
    const friendRequestBadge = document.getElementById("friendRequestBadge");
    const incomingCallModal = document.getElementById("incomingCallModal");
    const incomingCallerName = document.getElementById("incomingCallerName");

    let friends = [];
    let requests = [];
    let activeFriendId = null;
    let activeTab = "friends";
    let activeCallId = null;
    let friendPeer = null;
    let pendingIce = [];

    const rtcConfig = { iceServers: [{ urls: "stun:stun.l.google.com:19302" }] };

    const friendConnection = new signalR.HubConnectionBuilder()
        .withUrl("/friendHub")
        .withAutomaticReconnect()
        .build();

    function countryFlag(code) {
        if (!code || code === "XX" || code.length !== 2) {
            return "🌐";
        }
        return code.toUpperCase().replace(/./g, (char) =>
            String.fromCodePoint(127397 + char.charCodeAt(0)));
    }

    function openPanel() {
        friendsPanel?.removeAttribute("hidden");
        friendsBackdrop?.removeAttribute("hidden");
        friendConnection.invoke("GetFriendsSnapshot").catch(console.error);
    }

    function closePanel() {
        friendsPanel?.setAttribute("hidden", "");
        friendsBackdrop?.setAttribute("hidden", "");
        showFriendsList();
    }

    function setBadge(el, count) {
        if (!el) {
            return;
        }
        if (count > 0) {
            el.textContent = String(count);
            el.removeAttribute("hidden");
        } else {
            el.setAttribute("hidden", "");
        }
    }

    function showFriendsList() {
        friendChatView?.setAttribute("hidden", "");
        friendsTabContent?.removeAttribute("hidden");
        document.querySelector(".friends-tabs")?.removeAttribute("hidden");
        activeFriendId = null;
    }

    function showFriendChat(friend) {
        activeFriendId = friend.userId;
        friendChatTitle.textContent = friend.displayName;
        if (friend.isOnline) {
            friendChatOnline.removeAttribute("hidden");
        } else {
            friendChatOnline.setAttribute("hidden", "");
        }
        friendsTabContent.setAttribute("hidden", "");
        document.querySelector(".friends-tabs")?.setAttribute("hidden", "");
        friendChatView.removeAttribute("hidden");
        friendChatMessages.innerHTML = "";
        friendConnection.invoke("LoadFriendMessages", friend.userId).catch(console.error);
    }

    function renderFriends() {
        if (!friendsTabContent || activeTab !== "friends") {
            return;
        }

        if (friends.length === 0) {
            friendsTabContent.innerHTML = '<p class="friends-empty">No friends yet. Match with someone and send a friend request.</p>';
            return;
        }

        friendsTabContent.innerHTML = friends.map((friend) => `
            <div class="friend-list-item">
                <div class="friend-list-row">
                    <div>
                        <div class="friend-list-name">${countryFlag(friend.countryCode)} ${escapeHtml(friend.displayName)}</div>
                        <div class="friend-list-meta">${escapeHtml(friend.country)} · ${friend.isOnline ? "Online" : "Offline"}${friend.unreadCount > 0 ? ` · ${friend.unreadCount} new` : ""}</div>
                    </div>
                </div>
                <div class="friend-list-actions">
                    <button type="button" class="btn btn-sm btn-primary" data-action="chat" data-id="${friend.userId}">Message</button>
                    <button type="button" class="btn btn-sm btn-outline-primary" data-action="call" data-id="${friend.userId}" ${friend.isOnline ? "" : "disabled"}>Call</button>
                </div>
            </div>
        `).join("");

        friendsTabContent.querySelectorAll("button[data-action]").forEach((btn) => {
            btn.addEventListener("click", () => {
                const id = btn.dataset.id;
                const friend = friends.find((item) => item.userId === id);
                if (!friend) {
                    return;
                }
                if (btn.dataset.action === "chat") {
                    showFriendChat(friend);
                } else {
                    startFriendCall(id);
                }
            });
        });
    }

    function renderRequests() {
        if (!friendsTabContent || activeTab !== "requests") {
            return;
        }

        if (requests.length === 0) {
            friendsTabContent.innerHTML = '<p class="friends-empty">No pending friend requests.</p>';
            return;
        }

        friendsTabContent.innerHTML = requests.map((request) => `
            <div class="friend-request-item">
                <div class="friend-list-name">${countryFlag(request.countryCode)} ${escapeHtml(request.displayName)}</div>
                <div class="friend-list-meta">${escapeHtml(request.country)}</div>
                <div class="friend-list-actions">
                    <button type="button" class="btn btn-sm btn-success" data-accept="${request.requestId}">Accept</button>
                    <button type="button" class="btn btn-sm btn-outline-danger" data-decline="${request.requestId}">Decline</button>
                </div>
            </div>
        `).join("");

        friendsTabContent.querySelectorAll("[data-accept]").forEach((btn) => {
            btn.addEventListener("click", () => {
                friendConnection.invoke("AcceptFriendRequest", Number.parseInt(btn.dataset.accept, 10)).catch(console.error);
            });
        });

        friendsTabContent.querySelectorAll("[data-decline]").forEach((btn) => {
            btn.addEventListener("click", () => {
                friendConnection.invoke("DeclineFriendRequest", Number.parseInt(btn.dataset.decline, 10)).catch(console.error);
            });
        });
    }

    function escapeHtml(text) {
        const div = document.createElement("div");
        div.textContent = text;
        return div.innerHTML;
    }

    function appendFriendMessage(msg) {
        const row = document.createElement("div");
        row.className = `friend-msg ${msg.isMine ? "mine" : "theirs"}`;
        const time = new Date(msg.sentAtUtc).toLocaleTimeString([], { hour: "2-digit", minute: "2-digit" });
        row.textContent = `${msg.isMine ? "You" : "Friend"} (${time}): ${msg.body}`;
        friendChatMessages.appendChild(row);
        friendChatMessages.scrollTop = friendChatMessages.scrollHeight;
    }

    async function ensureMedia() {
        if (window.strangersCall?.ensureLocalStream) {
            return window.strangersCall.ensureLocalStream();
        }
        return navigator.mediaDevices.getUserMedia({ video: true, audio: true });
    }

    function getLocalStream() {
        return window.strangersCall?.getLocalStream?.() || null;
    }

    function closeFriendPeer() {
        if (!friendPeer) {
            return;
        }
        friendPeer.onicecandidate = null;
        friendPeer.ontrack = null;
        friendPeer.close();
        friendPeer = null;
        pendingIce = [];
    }

    async function flushFriendIce() {
        if (!friendPeer?.remoteDescription || pendingIce.length === 0) {
            return;
        }
        const queued = pendingIce;
        pendingIce = [];
        for (const candidate of queued) {
            await friendPeer.addIceCandidate(candidate);
        }
    }

    async function startFriendWebRtc(callId, isInitiator) {
        await ensureMedia();
        const localStream = getLocalStream();
        if (!localStream) {
            return;
        }

        closeFriendPeer();
        friendPeer = new RTCPeerConnection(rtcConfig);
        localStream.getTracks().forEach((track) => friendPeer.addTrack(track, localStream));

        friendPeer.onicecandidate = (event) => {
            if (!event.candidate || !activeCallId) {
                return;
            }
            friendConnection.invoke(
                "SendFriendIceCandidate",
                activeCallId,
                JSON.stringify(event.candidate),
                event.candidate.sdpMid,
                event.candidate.sdpMLineIndex
            ).catch(console.error);
        };

        friendPeer.ontrack = (event) => {
            const remoteVideo = document.getElementById("remoteVideo");
            if (event.streams?.[0] && remoteVideo) {
                remoteVideo.srcObject = event.streams[0];
                remoteVideo.play().catch(console.error);
            }
        };

        if (isInitiator) {
            const offer = await friendPeer.createOffer();
            await friendPeer.setLocalDescription(offer);
            await friendConnection.invoke("SendFriendOffer", callId, offer.sdp);
        }
    }

    async function startFriendCall(friendUserId) {
        const callId = await friendConnection.invoke("StartFriendCall", friendUserId);
        if (!callId) {
            return;
        }
        activeCallId = callId;
        closePanel();
    }

    document.getElementById("openFriendsBtn")?.addEventListener("click", openPanel);
    document.getElementById("closeFriendsBtn")?.addEventListener("click", closePanel);
    friendsBackdrop?.addEventListener("click", closePanel);
    document.getElementById("backToFriendsBtn")?.addEventListener("click", showFriendsList);

    document.querySelectorAll(".friends-tab").forEach((tab) => {
        tab.addEventListener("click", () => {
            document.querySelectorAll(".friends-tab").forEach((item) => item.classList.remove("active"));
            tab.classList.add("active");
            activeTab = tab.dataset.tab;
            if (activeTab === "friends") {
                renderFriends();
            } else {
                renderRequests();
            }
        });
    });

    document.getElementById("addFriendBtn")?.addEventListener("click", () => {
        const partnerId = window.strangersCall?.getPartnerUserId?.();
        if (!partnerId) {
            return;
        }
        friendConnection.invoke("SendFriendRequest", partnerId)
            .then(() => {
                const btn = document.getElementById("addFriendBtn");
                if (btn) {
                    btn.disabled = true;
                    btn.textContent = "Request sent";
                }
            })
            .catch(console.error);
    });

    document.getElementById("friendChatSendBtn")?.addEventListener("click", sendFriendChat);
    friendChatInput?.addEventListener("keydown", (event) => {
        if (event.key === "Enter") {
            sendFriendChat();
        }
    });

    document.getElementById("friendCallBtn")?.addEventListener("click", () => {
        if (activeFriendId) {
            startFriendCall(activeFriendId);
        }
    });

    document.getElementById("acceptCallBtn")?.addEventListener("click", () => {
        if (activeCallId) {
            friendConnection.invoke("AcceptFriendCall", activeCallId).catch(console.error);
            incomingCallModal.setAttribute("hidden", "");
        }
    });

    document.getElementById("declineCallBtn")?.addEventListener("click", () => {
        if (activeCallId) {
            friendConnection.invoke("DeclineFriendCall", activeCallId).catch(console.error);
            incomingCallModal.setAttribute("hidden", "");
            activeCallId = null;
        }
    });

    function sendFriendChat() {
        const text = friendChatInput?.value.trim();
        if (!text || !activeFriendId) {
            return;
        }
        friendConnection.invoke("SendFriendMessage", activeFriendId, text)
            .then(() => { friendChatInput.value = ""; })
            .catch(console.error);
    }

    friendConnection.on("FriendsSnapshot", (snapshot) => {
        friends = snapshot.friends || [];
        requests = snapshot.requests || [];
        setBadge(navFriendBadge, snapshot.pendingCount || 0);
        setBadge(friendRequestBadge, snapshot.pendingCount || 0);
        if (activeTab === "friends") {
            renderFriends();
        } else {
            renderRequests();
        }
    });

    friendConnection.on("FriendRequestReceived", () => {
        friendConnection.invoke("GetFriendsSnapshot").catch(console.error);
    });

    friendConnection.on("FriendRequestFailed", (message) => alert(message || "Could not send request."));
    friendConnection.on("FriendActionFailed", (message) => alert(message || "Action failed."));

    friendConnection.on("FriendMessagesLoaded", (friendUserId, messages) => {
        if (friendUserId !== activeFriendId) {
            return;
        }
        friendChatMessages.innerHTML = "";
        messages.forEach(appendFriendMessage);
    });

    friendConnection.on("FriendMessageReceived", (msg) => {
        if (msg.senderId === activeFriendId) {
            appendFriendMessage({ body: msg.body, isMine: false, sentAtUtc: msg.sentAtUtc });
        }
        friendConnection.invoke("GetFriendsSnapshot").catch(console.error);
    });

    friendConnection.on("FriendMessageSent", (msg) => {
        appendFriendMessage({ body: msg.body, isMine: true, sentAtUtc: msg.sentAtUtc });
    });

    friendConnection.on("IncomingFriendCall", (payload) => {
        activeCallId = payload.callId;
        incomingCallerName.textContent = payload.name || "Friend";
        incomingCallModal.removeAttribute("hidden");
    });

    friendConnection.on("FriendCallStarted", async (payload) => {
        activeCallId = payload.callId;
        await startFriendWebRtc(payload.callId, payload.isInitiator);
    });

    friendConnection.on("FriendCallAccepted", async (payload) => {
        if (activeCallId === payload.callId && friendPeer) {
            return;
        }
        await startFriendWebRtc(payload.callId, true);
    });

    friendConnection.on("FriendCallMissed", (payload) => {
        alert(`Call missed — ${payload.name} was offline. They will see the missed call when back online.`);
        activeCallId = null;
    });

    friendConnection.on("MissedFriendCalls", (missed) => {
        if (missed?.length > 0) {
            const names = missed.map((call) => call.displayName).join(", ");
            alert(`You have missed calls from: ${names}`);
        }
    });

    friendConnection.on("FriendCallEnded", () => {
        closeFriendPeer();
        activeCallId = null;
    });

    friendConnection.on("FriendCallDeclined", () => {
        alert("Call declined.");
        closeFriendPeer();
        activeCallId = null;
    });

    friendConnection.on("FriendCallCancelled", () => {
        closeFriendPeer();
        activeCallId = null;
    });

    friendConnection.on("ReceiveFriendOffer", async (callId, sdp) => {
        if (activeCallId !== callId) {
            activeCallId = callId;
        }
        await ensureMedia();
        if (!friendPeer) {
            await startFriendWebRtc(callId, false);
        }
        await friendPeer.setRemoteDescription({ type: "offer", sdp });
        await flushFriendIce();
        const answer = await friendPeer.createAnswer();
        await friendPeer.setLocalDescription(answer);
        await friendConnection.invoke("SendFriendAnswer", callId, answer.sdp);
    });

    friendConnection.on("ReceiveFriendAnswer", async (callId, sdp) => {
        if (!friendPeer) {
            return;
        }
        await friendPeer.setRemoteDescription({ type: "answer", sdp });
        await flushFriendIce();
    });

    friendConnection.on("ReceiveFriendIceCandidate", async (callId, candidateJson, sdpMid, sdpMLineIndex) => {
        if (!friendPeer || !candidateJson) {
            return;
        }
        const candidate = JSON.parse(candidateJson);
        const init = {
            candidate: candidate.candidate,
            sdpMid: sdpMid ?? candidate.sdpMid,
            sdpMLineIndex: sdpMLineIndex ?? candidate.sdpMLineIndex
        };
        if (!friendPeer.remoteDescription) {
            pendingIce.push(init);
            return;
        }
        await friendPeer.addIceCandidate(init);
    });

    friendConnection.start().catch(console.error);

    window.friendsApp = { openPanel, sendFriendRequest: (userId) => friendConnection.invoke("SendFriendRequest", userId) };
});
