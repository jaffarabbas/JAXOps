// IIS Manager — Dashboard monitoring JS
// Extends site.js — subscribes all server cards to health updates

(function () {
    const monConn = new signalR.HubConnectionBuilder()
        .withUrl('/hubs/monitoring')
        .withAutomaticReconnect()
        .build();

    monConn.on('OnServerHealthUpdate', (data) => {
        updateServerHealthCard(data);
    });

    monConn.on('OnServerStatusChange', (data) => {
        const card = document.getElementById(`server-card-${data.serverId}`);
        if (!card) return;
        const badge = card.querySelector('.badge');
        if (badge) {
            badge.textContent = data.status;
            badge.className = `badge ${data.status === 'Online' ? 'bg-success' : 'bg-danger'}`;
        }
    });

    monConn.start().then(() => {
        document.querySelectorAll('[id^="server-card-"]').forEach(el => {
            const id = parseInt(el.id.replace('server-card-', ''));
            if (!isNaN(id)) monConn.invoke('SubscribeToServer', id).catch(() => {});
        });
    });

    function updateServerHealthCard(data) {
        const card = document.getElementById(`server-card-${data.serverId}`);
        if (!card) return;

        const bars = card.querySelectorAll('.progress-bar');
        if (bars.length >= 2) {
            bars[0].style.width = data.cpuPercent + '%';
            bars[0].className = `progress-bar ${data.cpuPercent > 85 ? 'bg-danger' : 'bg-primary'}`;
            const ramPct = data.ramTotalMB > 0 ? data.ramUsedMB / data.ramTotalMB * 100 : 0;
            bars[1].style.width = ramPct.toFixed(1) + '%';
            bars[1].className = `progress-bar ${ramPct > 85 ? 'bg-danger' : 'bg-info'}`;
        }

        // CPU/RAM text
        const spans = card.querySelectorAll('.d-flex.justify-content-between.small span:last-child');
        if (spans.length >= 2) {
            spans[0].textContent = data.cpuPercent.toFixed(1) + '%';
            const ramPct = data.ramTotalMB > 0 ? (data.ramUsedMB / data.ramTotalMB * 100).toFixed(1) : 0;
            spans[1].textContent = ramPct + '%';
        }

        // Sites count
        const sitesEl = card.querySelector('.text-muted.small');
        if (sitesEl) sitesEl.textContent = `${data.runningSites} sites running`;

        card.classList.add('live-update');
        setTimeout(() => card.classList.remove('live-update'), 400);
    }
})();
