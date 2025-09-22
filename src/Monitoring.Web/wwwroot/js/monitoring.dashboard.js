(function () {
    const root = document.getElementById('monitoring-dashboard');
    if (!root) {
        return;
    }

    const defaultRangeDays = parseInt(root.dataset.defaultRange, 10) || 7;
    const maxRangeDays = parseInt(root.dataset.maxRange, 10) || 180;

    const rangeStartInput = document.getElementById('dashboard-range-start');
    const rangeEndInput = document.getElementById('dashboard-range-end');
    const typeSelect = document.getElementById('dashboard-type-filter');
    const targetSelect = document.getElementById('dashboard-target-picker');
    const applyButton = document.getElementById('dashboard-apply');
    const summaryEls = {
        total: document.getElementById('dashboard-total-targets'),
        online: document.getElementById('dashboard-online'),
        offline: document.getElementById('dashboard-offline'),
        uptime: document.getElementById('dashboard-uptime'),
        incidents: document.getElementById('dashboard-incidents-count'),
        checking: document.getElementById('dashboard-checking')
    };
    const reliabilityEl = document.getElementById('dashboard-reliability');
    const incidentsTable = document.getElementById('dashboard-incidents-table');
    const exportUptimeButton = document.getElementById('dashboard-export-uptime');
    const exportIncidentsButton = document.getElementById('dashboard-export-incidents');
    const presetButtons = Array.from(root.querySelectorAll('[data-range]'));
    const chartCanvas = document.getElementById('dashboard-uptime-chart');

    let targetsCache = [];
    let uptimeChart;

    function setDateInputs(from, to) {
        rangeStartInput.value = toLocalInputValue(from);
        rangeEndInput.value = toLocalInputValue(to);
    }

    function toLocalInputValue(date) {
        const local = new Date(date.getTime() - date.getTimezoneOffset() * 60000);
        return local.toISOString().slice(0, 16);
    }

    function parseInputValue(input) {
        if (!input.value) {
            return null;
        }

        const date = new Date(input.value);
        if (Number.isNaN(date.getTime())) {
            return null;
        }

        return date;
    }

    function clampRange(from, to) {
        if (to <= from) {
            to = new Date(from.getTime() + 60 * 60 * 1000);
        }

        const maxMillis = maxRangeDays * 24 * 60 * 60 * 1000;
        if (to.getTime() - from.getTime() > maxMillis) {
            from = new Date(to.getTime() - maxMillis);
        }

        return { from, to };
    }

    function formatPercent(value) {
        if (value === null || value === undefined) {
            return '—';
        }

        return `${value.toFixed(2)}%`;
    }

    function formatDuration(seconds) {
        if (!seconds || seconds <= 0) {
            return '—';
        }

        const hours = Math.floor(seconds / 3600);
        const minutes = Math.floor((seconds % 3600) / 60);
        const secs = Math.floor(seconds % 60);
        const parts = [];
        if (hours) {
            parts.push(`${hours}h`);
        }

        if (minutes) {
            parts.push(`${minutes}m`);
        }

        if (secs && parts.length < 2) {
            parts.push(`${secs}s`);
        }

        return parts.join(' ') || `${secs}s`;
    }

    function formatDate(value) {
        if (!value) {
            return '—';
        }

        const date = new Date(value);
        if (Number.isNaN(date.getTime())) {
            return '—';
        }

        return date.toLocaleString();
    }

    function getSelectedType() {
        const value = typeSelect.value;
        if (!value || value === 'all') {
            return null;
        }

        return value;
    }

    function getSelectedTargetIds() {
        const value = targetSelect.value;
        if (!value || value === 'all') {
            return targetsCache.map(x => x.id);
        }

        return [value];
    }

    function determineBucket(from, to) {
        const diff = to.getTime() - from.getTime();
        const hours = diff / (60 * 60 * 1000);
        return hours <= 48 ? 'hour' : 'day';
    }

    async function fetchJson(url) {
        const response = await fetch(url, {
            headers: {
                Accept: 'application/json'
            }
        });

        if (!response.ok) {
            throw new Error(`Request failed (${response.status})`);
        }

        return await response.json();
    }

    function buildQuery(params) {
        const searchParams = new URLSearchParams();
        Object.entries(params).forEach(([key, value]) => {
            if (value !== undefined && value !== null && value !== '') {
                searchParams.append(key, value);
            }
        });
        return searchParams.toString();
    }

    async function loadTargets() {
        const type = getSelectedType();
        const query = type ? `?type=${encodeURIComponent(type)}` : '';
        const items = await fetchJson(`/api/monitoring/targets/overview${query}`);
        targetsCache = items.map(item => ({ id: item.id, name: item.name }));

        targetSelect.innerHTML = '<option value="all">All targets</option>';
        targetsCache.forEach(item => {
            const option = document.createElement('option');
            option.value = item.id;
            option.textContent = item.name;
            targetSelect.appendChild(option);
        });
    }

    async function loadSummary(from, to, type) {
        const query = buildQuery({
            from: from.toISOString(),
            to: to.toISOString(),
            type
        });

        const summary = await fetchJson(`/api/monitoring/dashboard/summary?${query}`);
        summaryEls.total.textContent = summary.totalTargets ?? 0;
        summaryEls.online.textContent = summary.onlineCount ?? 0;
        summaryEls.offline.textContent = summary.offlineCount ?? 0;
        summaryEls.uptime.textContent = formatPercent(summary.uptimePercentage ?? 100);
        summaryEls.incidents.textContent = summary.incidentsCount ?? 0;
        summaryEls.checking.textContent = summary.checkingCount ?? 0;
    }

    async function loadReliability(from, to, type) {
        reliabilityEl.textContent = 'Loading…';
        try {
            const query = buildQuery({
                from: from.toISOString(),
                to: to.toISOString(),
                type
            });
            const data = await fetchJson(`/api/monitoring/dashboard/mttr-mtbf?${query}`);
            const lines = [];
            if (data.meanTimeToRecoverSeconds != null) {
                lines.push(`MTTR: ${formatDuration(data.meanTimeToRecoverSeconds)}`);
            }
            if (data.meanTimeBetweenFailuresSeconds != null) {
                lines.push(`MTBF: ${formatDuration(data.meanTimeBetweenFailuresSeconds)}`);
            }

            if (Array.isArray(data.breakdown) && data.breakdown.length) {
                lines.push('');
                data.breakdown.forEach(item => {
                    const mttr = item.meanTimeToRecoverSeconds != null ? formatDuration(item.meanTimeToRecoverSeconds) : '—';
                    const mtbf = item.meanTimeBetweenFailuresSeconds != null ? formatDuration(item.meanTimeBetweenFailuresSeconds) : '—';
                    lines.push(`${item.serviceType}: MTTR ${mttr}, MTBF ${mtbf}`);
                });
            }

            reliabilityEl.innerHTML = lines.length ? lines.join('<br>') : 'No outage data in the selected range.';
        } catch (error) {
            reliabilityEl.textContent = 'Unable to load reliability metrics.';
            console.error(error);
        }
    }

    async function loadUptimeAndIncidents(from, to) {
        const bucket = determineBucket(from, to);
        const targetIds = getSelectedTargetIds();
        if (!targetIds.length) {
            clearChart();
            renderIncidents([]);
            return;
        }

        const seriesResponses = await Promise.all(
            targetIds.map(id => fetchJson(`/api/monitoring/dashboard/uptime/${id}?${buildQuery({
                from: from.toISOString(),
                to: to.toISOString(),
                bucket
            })}`))
        );

        let mergedSeries = [];
        if (seriesResponses.length === 1) {
            mergedSeries = seriesResponses[0];
        } else if (seriesResponses.length > 1 && seriesResponses[0].length) {
            const length = seriesResponses[0].length;
            mergedSeries = seriesResponses[0].map((bucketItem, index) => {
                const average = seriesResponses.reduce((sum, series) => {
                    const item = series[index];
                    return sum + (item ? item.uptimePercentage ?? 0 : 0);
                }, 0) / seriesResponses.length;
                return {
                    start: bucketItem.start,
                    end: bucketItem.end,
                    uptimePercentage: Math.round(average * 100) / 100
                };
            });
        }

        renderChart(mergedSeries, bucket);

        const incidentResponses = await Promise.all(
            targetIds.map(id => fetchJson(`/api/monitoring/dashboard/incidents/${id}?${buildQuery({
                from: from.toISOString(),
                to: to.toISOString(),
                max: 50
            })}`))
        );

        const incidents = incidentResponses
            .flat()
            .sort((a, b) => new Date(b.startedAt) - new Date(a.startedAt))
            .slice(0, 50);

        renderIncidents(incidents);
    }

    function renderChart(series, bucket) {
        const labels = series.map(item => new Date(item.start).toLocaleString());
        const data = series.map(item => item.uptimePercentage ?? 0);

        if (!uptimeChart) {
            uptimeChart = new Chart(chartCanvas, {
                type: 'line',
                data: {
                    labels,
                    datasets: [
                        {
                            label: 'Uptime %',
                            data,
                            fill: true,
                            tension: 0.3,
                            borderColor: 'rgba(37, 99, 235, 1)',
                            backgroundColor: 'rgba(37, 99, 235, 0.15)',
                            pointRadius: 2
                        }
                    ]
                },
                options: {
                    responsive: true,
                    scales: {
                        y: {
                            min: 0,
                            max: 100,
                            ticks: {
                                callback: (value) => `${value}%`
                            }
                        }
                    },
                    plugins: {
                        legend: {
                            display: false
                        },
                        tooltip: {
                            callbacks: {
                                label: (context) => `${context.formattedValue}% uptime`
                            }
                        }
                    }
                }
            });
        } else {
            uptimeChart.data.labels = labels;
            uptimeChart.data.datasets[0].data = data;
            uptimeChart.update();
        }

        chartCanvas.setAttribute('aria-label', `Uptime trend per ${bucket}`);
    }

    function clearChart() {
        if (uptimeChart) {
            uptimeChart.destroy();
            uptimeChart = null;
        }
        chartCanvas.setAttribute('aria-label', 'Uptime trend');
    }

    function renderIncidents(incidents) {
        const tbody = incidentsTable.querySelector('tbody');
        tbody.innerHTML = '';

        if (!incidents.length) {
            const row = document.createElement('tr');
            row.className = 'text-muted';
            const cell = document.createElement('td');
            cell.colSpan = 4;
            cell.textContent = 'No incidents in range.';
            row.appendChild(cell);
            tbody.appendChild(row);
            return;
        }

        incidents.forEach(item => {
            const row = document.createElement('tr');
            row.innerHTML = `
                <td>${formatDate(item.startedAt)}</td>
                <td>${item.endedAt ? formatDate(item.endedAt) : 'Ongoing'}</td>
                <td>${formatDuration(item.totalDurationSec)}</td>
                <td>${item.failureCount ?? 0}</td>`;
            tbody.appendChild(row);
        });
    }

    function bindExportButtons() {
        exportUptimeButton.addEventListener('click', () => {
            const { from, to } = getCurrentRange();
            const bucket = determineBucket(from, to);
            const targetIds = getSelectedTargetIds();
            if (!targetIds.length) {
                abp.notify.warn('No targets selected for export.');
                return;
            }

            if (targetIds.length > 1) {
                abp.notify.info('Exporting the first target when "All" is selected.');
            }

            const target = targetIds[0];
            const query = buildQuery({ from: from.toISOString(), to: to.toISOString(), bucket });
            window.location.href = `/api/monitoring/export/uptime/${target}.csv?${query}`;
        });

        exportIncidentsButton.addEventListener('click', () => {
            const { from, to } = getCurrentRange();
            const targetIds = getSelectedTargetIds();
            if (!targetIds.length) {
                abp.notify.warn('No targets selected for export.');
                return;
            }

            if (targetIds.length > 1) {
                abp.notify.info('Exporting the first target when "All" is selected.');
            }

            const target = targetIds[0];
            const query = buildQuery({ from: from.toISOString(), to: to.toISOString() });
            window.location.href = `/api/monitoring/export/incidents/${target}.csv?${query}`;
        });
    }

    function getCurrentRange() {
        let from = parseInputValue(rangeStartInput) || new Date(Date.now() - defaultRangeDays * 24 * 60 * 60 * 1000);
        let to = parseInputValue(rangeEndInput) || new Date();
        return clampRange(from, to);
    }

    async function refresh() {
        try {
            const { from, to } = getCurrentRange();
            setDateInputs(from, to);
            const type = getSelectedType();
            await loadSummary(from, to, type);
            await loadReliability(from, to, type);
            await loadUptimeAndIncidents(from, to);
        } catch (error) {
            console.error(error);
            abp.notify.error('Failed to load dashboard data.');
        }
    }

    function bindInteractions() {
        typeSelect.addEventListener('change', async () => {
            try {
                await loadTargets();
                await refresh();
            } catch (error) {
                console.error(error);
                abp.notify.error('Unable to refresh target list.');
            }
        });

        applyButton.addEventListener('click', refresh);

        presetButtons.forEach(button => {
            button.addEventListener('click', () => {
                const preset = parseInt(button.dataset.range, 10);
                const to = new Date();
                let from;
                if (preset <= 48) {
                    from = new Date(to.getTime() - preset * 60 * 60 * 1000);
                } else {
                    from = new Date(to.getTime() - preset * 24 * 60 * 60 * 1000);
                }

                const clamped = clampRange(from, to);
                setDateInputs(clamped.from, clamped.to);
                refresh();
            });
        });
    }

    (async function initialize() {
        const now = new Date();
        const from = new Date(now.getTime() - defaultRangeDays * 24 * 60 * 60 * 1000);
        const clamped = clampRange(from, now);
        setDateInputs(clamped.from, clamped.to);
        bindInteractions();
        bindExportButtons();
        await loadTargets();
        await refresh();
    })();
})();
