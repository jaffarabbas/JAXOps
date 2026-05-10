// IIS Manager — Deployment Console SignalR client

function initDeploymentConsole(deploymentId) {
    const conn = new signalR.HubConnectionBuilder()
        .withUrl('/hubs/deployment')
        .withAutomaticReconnect()
        .build();

    const logContainer = document.getElementById('log-container');
    const statusBadge = document.getElementById('status-badge');
    const liveIndicator = document.getElementById('live-indicator');

    conn.on('OnLogLine', (data) => {
        if (data.deploymentId !== deploymentId) return;
        appendLogLine(data.timestamp, data.message, data.level);
    });

    conn.on('OnProgress', (data) => {
        if (data.deploymentId !== deploymentId) return;
        const bar = document.getElementById(`target-progress-${data.serverId}`);
        if (bar) bar.style.width = data.percent + '%';
    });

    conn.on('OnStatusChange', (data) => {
        if (data.deploymentId !== deploymentId) return;
        if (statusBadge) {
            statusBadge.textContent = data.status;
            statusBadge.className = `badge ${statusBadge_css(data.status)}`;
        }
        if (liveIndicator && (data.status === 'Succeeded' || data.status === 'Failed')) {
            liveIndicator.innerHTML = '<span class="text-white-50 small">Completed</span>';
        }
        const targetBadge = document.getElementById(`target-status-${data.serverId}`);
        if (targetBadge) {
            targetBadge.textContent = data.status;
            targetBadge.className = `badge ${statusBadge_css(data.status)}`;
        }
    });

    conn.start()
        .then(() => conn.invoke('SubscribeToDeployment', deploymentId))
        .catch(console.error);

    // Poll status every 5 s while InProgress — catches failures that happen before SignalR is subscribed
    let pollTimer = setInterval(async () => {
        try {
            const resp = await fetch(`/api/deployments/${deploymentId}/status`);
            if (!resp.ok) return;
            const data = await resp.json();
            if (statusBadge) {
                statusBadge.textContent = data.status;
                statusBadge.className = `badge ${statusBadge_css(data.status)}`;
            }
            if (data.status !== 'Queued' && data.status !== 'InProgress') {
                clearInterval(pollTimer);
                if (liveIndicator)
                    liveIndicator.innerHTML = '<span class="text-white-50 small">Completed</span>';
            }
        } catch { /* ignore */ }
    }, 5000);

    function appendLogLine(timestamp, message, level) {
        if (!logContainer) return;
        const div = document.createElement('div');
        div.className = `px-3 py-1 log-line ${levelClass(level)}`;
        div.style.cssText = 'font-family:monospace;font-size:0.82rem';
        const ts = timestamp ? new Date(timestamp).toTimeString().substring(0, 8) : '';
        div.innerHTML = `<span class="text-white-50">${ts}</span> ${escHtml(message)}`;
        logContainer.appendChild(div);
        logContainer.scrollTop = logContainer.scrollHeight;
    }

    function levelClass(level) {
        switch ((level || '').toLowerCase()) {
            case 'error': return 'log-error';
            case 'warning': return 'log-warning';
            default: return 'text-white-50';
        }
    }

    function statusBadge_css(status) {
        switch (status) {
            case 'Succeeded': return 'bg-success';
            case 'Failed': return 'bg-danger';
            case 'InProgress': return 'bg-primary';
            default: return 'bg-secondary';
        }
    }

    function escHtml(str) {
        return (str || '').replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;');
    }
}
