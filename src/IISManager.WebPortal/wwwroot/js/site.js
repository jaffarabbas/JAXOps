// IIS Manager — Global site JS

// ── Monitoring Hub (all pages) ─────────────────────────────────────────────
(function () {
    const monConn = new signalR.HubConnectionBuilder()
        .withUrl('/hubs/monitoring')
        .withAutomaticReconnect()
        .build();

    monConn.onreconnecting(() => updateConnIndicator(false));
    monConn.onreconnected(() => updateConnIndicator(true));
    monConn.onclose(() => updateConnIndicator(false));

    monConn.on('OnServerStatusChange', (data) => {
        updateServerCard(data);
    });

    monConn.start()
        .then(() => {
            updateConnIndicator(true);
            // Subscribe to all servers visible on page
            document.querySelectorAll('[id^="server-card-"]').forEach(el => {
                const id = parseInt(el.id.replace('server-card-', ''));
                monConn.invoke('SubscribeToServer', id).catch(() => {});
            });
        })
        .catch(() => updateConnIndicator(false));

    function updateConnIndicator(online) {
        const el = document.getElementById('conn-indicator');
        const label = document.getElementById('conn-label');
        if (!el) return;
        el.className = `bi bi-circle-fill me-1 ${online ? 'text-success' : 'text-danger'}`;
        if (label) label.textContent = online ? 'Connected' : 'Reconnecting...';
    }

    function updateServerCard(data) {
        const card = document.getElementById(`server-card-${data.serverId}`);
        if (!card) return;
        card.classList.add('live-update');
        setTimeout(() => card.classList.remove('live-update'), 400);

        // Update status badge
        const badge = card.querySelector('.badge');
        if (badge) {
            badge.textContent = data.status;
            badge.className = `badge ${data.status === 'Online' ? 'bg-success' : 'bg-danger'}`;
        }
    }
})();
