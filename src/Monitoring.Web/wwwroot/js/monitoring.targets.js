(function (window) {
    const statusLabelMap = {
        0: 'Online',
        1: 'Offline',
        2: 'Checking',
        online: 'Online',
        offline: 'Offline',
        checking: 'Checking'
    };

    const statusClassMap = {
        Online: 'status-online',
        Offline: 'status-offline',
        Checking: 'status-checking'
    };

    const typeLabelMap = {
        0: 'Website',
        1: 'API',
        2: 'TCP',
        3: 'Redis',
        Website: 'Website',
        Api: 'API',
        API: 'API',
        Tcp: 'TCP',
        TCP: 'TCP',
        Redis: 'Redis'
    };

    const module = {
        init(options) {
            this.container = options.container;
            this.filterSelect = options.filterSelect;
            this.checkAllButton = options.checkAllButton;
            this.outagesModalEl = options.outagesModal;
            this.outagesModalTitle = options.outagesModalTitle;
            this.outagesTableBody = options.outagesTableBody;
            this.editModalEl = options.editModal;
            this.editForm = options.editForm;
            this.editModalTitle = options.editModalTitle;
            this.permissions = window.monitoringPermissions || { canRun: false, canEdit: false, canDelete: false };

            this.pulseTimers = [];
            this.targetCache = new Map();
            this.currentFilter = 'all';
            this.editingTargetId = null;
            this.relativeTimeFormatter = new Intl.RelativeTimeFormat(undefined, { numeric: 'auto' });

            if (this.filterSelect) {
                this.filterSelect.addEventListener('change', () => {
                    const value = this.filterSelect.value || 'all';
                    this.fetchTargets(value);
                });
            }

            if (this.checkAllButton && !this.permissions.canRun) {
                this.checkAllButton.classList.add('disabled');
                this.checkAllButton.setAttribute('disabled', 'disabled');
            }

            if (this.checkAllButton && this.permissions.canRun) {
                this.checkAllButton.addEventListener('click', (event) => {
                    event.preventDefault();
                    this.checkAllNow();
                });
            }

            if (this.outagesModalEl) {
                this.outagesModal = new bootstrap.Modal(this.outagesModalEl);
            }

            if (this.editModalEl) {
                this.editModal = new bootstrap.Modal(this.editModalEl);
            }

            if (this.editForm) {
                this.editForm.addEventListener('submit', (event) => this.submitEdit(event));
            }

            this.fetchTargets(this.currentFilter);
        },

        fetchTargets(typeValue) {
            if (!this.container) {
                return;
            }

            this.currentFilter = typeValue || this.currentFilter || 'all';
            this.clearPulseTimers();

            let url = '/api/monitoring/targets/overview';
            if (this.currentFilter && this.currentFilter !== 'all') {
                const params = new URLSearchParams();
                params.append('type', this.currentFilter);
                url += `?${params.toString()}`;
            }

            fetch(url, {
                headers: {
                    Accept: 'application/json'
                }
            })
                .then((response) => this.ensureSuccess(response))
                .then((data) => {
                    const targets = Array.isArray(data) ? data : [];
                    this.renderTargets(targets);
                })
                .catch((error) => this.handleError(error, 'Failed to load monitoring targets.'));
        },

        renderTargets(targets) {
            this.targetCache.clear();

            if (!targets.length) {
                this.container.innerHTML = '<div class="col-12"><div class="alert alert-info mb-0">No monitoring services found.</div></div>';
                return;
            }

            const cardsHtml = targets
                .map((target) => {
                    this.targetCache.set(target.id, target);
                    return this.renderCard(target);
                })
                .join('');

            this.container.innerHTML = cardsHtml;

            targets.forEach((target) => {
                const card = document.getElementById(`monitoring-card-${target.id}`);
                if (card) {
                    const interval = Number(target.checkIntervalSeconds) || 0;
                    this.startPulseTimer(card, interval);
                }
            });
        },

        renderCard(target) {
            const status = this.resolveStatus(target.currentStatus);
            const typeLabel = this.resolveType(target.type);
            const statusClass = statusClassMap[status] || statusClassMap.Checking;
            const lastChecked = this.relativeTime(target.lastCheckedAt);
            const nextDue = this.relativeTime(target.nextDueAt);

            const actions = [];
            if (this.permissions.canRun) {
                actions.push(`<button type="button" class="btn btn-outline-primary btn-sm" data-action="check" data-id="${target.id}"><i class="fa fa-sync me-1"></i>Check Now</button>`);
            }

            actions.push(`<button type="button" class="btn btn-outline-secondary btn-sm" data-action="outages" data-id="${target.id}"><i class="fa fa-bolt me-1"></i>View outages</button>`);

            if (this.permissions.canEdit) {
                actions.push(`<button type="button" class="btn btn-outline-info btn-sm" data-action="edit" data-id="${target.id}"><i class="fa fa-pen me-1"></i>Edit</button>`);
            }

            if (this.permissions.canDelete) {
                actions.push(`<button type="button" class="btn btn-outline-danger btn-sm" data-action="delete" data-id="${target.id}"><i class="fa fa-trash me-1"></i>Delete</button>`);
            }

            const escapedName = this.escapeHtml(target.name);
            const escapedEndpoint = this.escapeHtml(target.endpoint);

            return `
                <div class="col-12 col-md-6 col-xl-4">
                    <div class="status-border ${statusClass} h-100 p-3 bg-white shadow-sm" id="monitoring-card-${target.id}" data-target-id="${target.id}">
                        <div class="d-flex justify-content-between align-items-start">
                            <div>
                                <h5 class="mb-1">${escapedName}</h5>
                                <span class="badge bg-light text-dark">${typeLabel}</span>
                            </div>
                        </div>
                        <div class="mt-3 small">
                            <div class="mb-1"><span class="text-muted">Endpoint:</span> <span class="text-break">${escapedEndpoint}</span></div>
                            <div class="mb-1"><span class="text-muted">Status:</span> <span data-role="status-label">${status}</span></div>
                            <div class="mb-1"><span class="text-muted">Last checked:</span> <span data-role="last-checked">${lastChecked}</span></div>
                            <div><span class="text-muted">Next check:</span> <span data-role="next-due">${nextDue}</span></div>
                        </div>
                        <div class="mt-3 d-flex flex-wrap gap-2">
                            ${actions.join('')}
                        </div>
                    </div>
                </div>`;
        },

        relativeTime(iso) {
            if (!iso) {
                return '—';
            }

            const date = new Date(iso);
            if (Number.isNaN(date.getTime())) {
                return '—';
            }

            const now = new Date();
            let diff = date.getTime() - now.getTime();
            const absDiff = Math.abs(diff);
            let unit = 'second';
            let value = Math.round(diff / 1000);

            if (absDiff >= 1000 * 60 * 60 * 24) {
                unit = 'day';
                value = Math.round(diff / (1000 * 60 * 60 * 24));
            } else if (absDiff >= 1000 * 60 * 60) {
                unit = 'hour';
                value = Math.round(diff / (1000 * 60 * 60));
            } else if (absDiff >= 1000 * 60) {
                unit = 'minute';
                value = Math.round(diff / (1000 * 60));
            }

            return this.relativeTimeFormatter.format(value, unit);
        },

        startPulseTimer(card, intervalSeconds) {
            if (!intervalSeconds || intervalSeconds <= 0) {
                return;
            }

            const timer = setInterval(() => {
                card.classList.add('pulse-now');
                setTimeout(() => card.classList.remove('pulse-now'), 300);
            }, intervalSeconds * 1000);

            this.pulseTimers.push(timer);
        },

        clearPulseTimers() {
            this.pulseTimers.forEach((timer) => clearInterval(timer));
            this.pulseTimers = [];
        },

        checkAllNow() {
            if (!this.permissions.canRun) {
                return;
            }

            this.toggleButtonState(this.checkAllButton, true);
            this.request('/api/monitoring/check-all', { method: 'POST' })
                .then((count) => {
                    abp.notify.success(`Queued ${count ?? 0} service checks.`);
                    this.fetchTargets(this.currentFilter);
                })
                .catch((error) => this.handleError(error, 'Failed to queue monitoring checks.'))
                .finally(() => this.toggleButtonState(this.checkAllButton, false));
        },

        checkNow(id) {
            if (!this.permissions.canRun) {
                return;
            }

            this.setCardStatus(id, 'Checking');

            this.request(`/api/monitoring/targets/${id}/check`, { method: 'POST' })
                .then(() => {
                    abp.notify.success('Health check started.');
                    this.fetchTargets(this.currentFilter);
                })
                .catch((error) => this.handleError(error, 'Failed to start health check.'));
        },

        showOutages(id) {
            const target = this.targetCache.get(id);
            if (!target) {
                return;
            }

            if (this.outagesModalTitle) {
                this.outagesModalTitle.textContent = `${target.name} — recent outages`;
            }

            if (this.outagesTableBody) {
                this.outagesTableBody.innerHTML = '<tr><td colspan="4" class="text-center text-muted">Loading...</td></tr>';
            }

            this.getOutages(id)
                .then((outages) => {
                    this.renderOutages(outages);
                })
                .catch((error) => this.handleError(error, 'Failed to load outage history.'))
                .finally(() => {
                    if (this.outagesModal) {
                        this.outagesModal.show();
                    }
                });
        },

        getOutages(id, count = 10) {
            const url = `/api/monitoring/targets/${id}/outages?count=${count}`;
            return this.request(url, { method: 'GET' });
        },

        renderOutages(outages) {
            if (!this.outagesTableBody) {
                return;
            }

            if (!outages || !outages.length) {
                this.outagesTableBody.innerHTML = '<tr><td colspan="4" class="text-center text-muted">No outages recorded.</td></tr>';
                return;
            }

            const rows = outages
                .map((outage) => {
                    const started = this.formatDateTime(outage.startedAt);
                    const ended = outage.endedAt ? this.formatDateTime(outage.endedAt) : 'Ongoing';
                    const durationSeconds = outage.totalDurationSec ?? this.calculateDurationSeconds(outage.startedAt, outage.endedAt);
                    const duration = this.formatDuration(durationSeconds);
                    const failures = outage.failureCount ?? 0;

                    return `<tr>
                        <td>${started}</td>
                        <td>${ended}</td>
                        <td>${duration}</td>
                        <td>${failures}</td>
                    </tr>`;
                })
                .join('');

            this.outagesTableBody.innerHTML = rows;
        },

        openEdit(id) {
            if (!this.permissions.canEdit || !this.editModal) {
                return;
            }

            const target = this.targetCache.get(id);
            if (!target) {
                return;
            }

            this.editingTargetId = id;

            this.editForm.querySelector('#monitoring-edit-name').value = target.name ?? '';
            this.editForm.querySelector('#monitoring-edit-endpoint').value = target.endpoint ?? '';
            this.editForm.querySelector('#monitoring-edit-interval').value = target.checkIntervalSeconds ?? 0;
            this.editForm.querySelector('#monitoring-edit-timeout').value = target.timeoutSeconds ?? 0;
            this.editForm.querySelector('#monitoring-edit-retries').value = target.maxRetryAttempts ?? 0;
            this.editForm.querySelector('#monitoring-edit-retry-delay').value = target.retryDelaySeconds ?? 0;
            this.editForm.querySelector('#monitoring-edit-category').value = target.category ?? '';
            this.editForm.querySelector('#monitoring-edit-is-active').checked = Boolean(target.isActive);

            if (this.editModalTitle) {
                this.editModalTitle.textContent = `Edit ${target.name}`;
            }

            this.editModal.show();
        },

        submitEdit(event) {
            event.preventDefault();

            if (!this.permissions.canEdit || !this.editingTargetId) {
                return;
            }

            const target = this.targetCache.get(this.editingTargetId);
            if (!target) {
                return;
            }

            const formData = new FormData(this.editForm);
            const payload = {
                name: formData.get('name') || target.name,
                type: target.type,
                endpoint: formData.get('endpoint') || target.endpoint,
                settingsJson: target.settingsJson ?? null,
                checkIntervalSeconds: this.toNumber(formData.get('checkIntervalSeconds'), target.checkIntervalSeconds),
                timeoutSeconds: this.toNumber(formData.get('timeoutSeconds'), target.timeoutSeconds),
                maxRetryAttempts: this.toNumber(formData.get('maxRetryAttempts'), target.maxRetryAttempts),
                retryDelaySeconds: this.toNumber(formData.get('retryDelaySeconds'), target.retryDelaySeconds),
                category: this.normalizeString(formData.get('category')),
                isActive: formData.get('isActive') === 'on',
                currentStatus: target.currentStatus,
                lastCheckedAt: target.lastCheckedAt,
                lastStatusChangeAt: target.lastStatusChangeAt,
                nextDueAt: target.nextDueAt,
                consecutiveFailures: target.consecutiveFailures,
                firstDownAt: target.firstDownAt,
                lastUpAt: target.lastUpAt
            };

            this.toggleButtonState(this.editForm.querySelector('button[type="submit"]'), true);

            this.request(`/api/monitoring/targets/${this.editingTargetId}`, {
                method: 'PUT',
                body: payload
            })
                .then(() => {
                    abp.notify.success('Monitoring service updated.');
                    this.editModal.hide();
                    this.fetchTargets(this.currentFilter);
                })
                .catch((error) => this.handleError(error, 'Failed to update monitoring service.'))
                .finally(() => this.toggleButtonState(this.editForm.querySelector('button[type="submit"]'), false));
        },

        deleteTarget(id) {
            if (!this.permissions.canDelete) {
                return;
            }

            abp.message.confirm('This will deactivate the monitoring target. Continue?', 'Delete service')
                .then((confirmed) => {
                    if (!confirmed) {
                        return;
                    }

                    this.request(`/api/monitoring/targets/${id}`, { method: 'DELETE' })
                        .then(() => {
                            abp.notify.success('Monitoring service deleted.');
                            this.fetchTargets(this.currentFilter);
                        })
                        .catch((error) => this.handleError(error, 'Failed to delete monitoring service.'));
                });
        },

        setCardStatus(id, statusLabel) {
            const card = document.getElementById(`monitoring-card-${id}`);
            if (!card) {
                return;
            }

            const normalizedStatus = statusLabelMap[statusLabel?.toLowerCase?.()] || statusLabel;
            const status = normalizedStatus || 'Checking';
            const statusClass = statusClassMap[status] || statusClassMap.Checking;

            card.classList.remove('status-online', 'status-offline', 'status-checking');
            card.classList.add(statusClass);

            const labelEl = card.querySelector('[data-role="status-label"]');
            if (labelEl) {
                labelEl.textContent = status;
            }
        },

        toggleButtonState(button, isLoading) {
            if (!button) {
                return;
            }

            if (isLoading) {
                button.setAttribute('disabled', 'disabled');
                button.dataset.originalText = button.dataset.originalText || button.innerHTML;
                button.innerHTML = '<span class="spinner-border spinner-border-sm" role="status" aria-hidden="true"></span>';
            } else {
                button.removeAttribute('disabled');
                if (button.dataset.originalText) {
                    button.innerHTML = button.dataset.originalText;
                    delete button.dataset.originalText;
                }
            }
        },

        ensureSuccess(response) {
            if (response.ok) {
                if (response.status === 204) {
                    return null;
                }

                const contentType = response.headers.get('content-type') || '';
                if (contentType.includes('application/json')) {
                    return response.json();
                }

                return response.text();
            }

            throw response;
        },

        request(url, options) {
            const requestOptions = Object.assign({
                method: 'GET',
                headers: {
                    Accept: 'application/json'
                }
            }, options || {});

            requestOptions.method = (requestOptions.method || 'GET').toUpperCase();

            if (requestOptions.method !== 'GET' && requestOptions.method !== 'HEAD') {
                requestOptions.headers['Content-Type'] = 'application/json';
                if (window.abp?.security?.antiForgery) {
                    requestOptions.headers['RequestVerificationToken'] = window.abp.security.antiForgery.getToken();
                }

                if (requestOptions.body && typeof requestOptions.body !== 'string') {
                    requestOptions.body = JSON.stringify(requestOptions.body);
                } else if (!requestOptions.body) {
                    requestOptions.body = '{}';
                }
            }

            return fetch(url, requestOptions).then((response) => this.ensureSuccess(response));
        },

        handleError(error, fallbackMessage) {
            if (error instanceof Response) {
                return error
                    .clone()
                    .json()
                    .then((payload) => {
                        const message = payload?.error?.message || payload?.message || fallbackMessage;
                        abp.notify.error(message);
                    })
                    .catch(() => {
                        error.text().then((text) => {
                            const message = text || fallbackMessage;
                            abp.notify.error(message);
                        });
                    });
            }

            const message = error?.message || fallbackMessage;
            abp.notify.error(message);
        },

        resolveStatus(value) {
            if (value === null || value === undefined) {
                return 'Checking';
            }

            if (typeof value === 'string') {
                const normalized = value.trim();
                return statusLabelMap[normalized] || statusLabelMap[normalized.toLowerCase()] || normalized;
            }

            if (typeof value === 'number') {
                return statusLabelMap[value] || 'Checking';
            }

            return 'Checking';
        },

        resolveType(value) {
            if (value === null || value === undefined) {
                return 'Unknown';
            }

            if (typeof value === 'string') {
                const normalized = value.trim();
                return typeLabelMap[normalized] || typeLabelMap[normalized.charAt(0).toUpperCase() + normalized.slice(1)] || normalized;
            }

            if (typeof value === 'number') {
                return typeLabelMap[value] || 'Unknown';
            }

            return 'Unknown';
        },

        formatDateTime(value) {
            if (!value) {
                return '—';
            }

            const date = new Date(value);
            if (Number.isNaN(date.getTime())) {
                return '—';
            }

            return date.toLocaleString();
        },

        calculateDurationSeconds(startedAt, endedAt) {
            const start = new Date(startedAt);
            const end = endedAt ? new Date(endedAt) : new Date();

            if (Number.isNaN(start.getTime()) || Number.isNaN(end.getTime())) {
                return 0;
            }

            const seconds = Math.max(0, Math.round((end.getTime() - start.getTime()) / 1000));
            return seconds;
        },

        formatDuration(seconds) {
            if (seconds === null || seconds === undefined || seconds < 0) {
                return '—';
            }

            const hrs = Math.floor(seconds / 3600)
                .toString()
                .padStart(2, '0');
            const mins = Math.floor((seconds % 3600) / 60)
                .toString()
                .padStart(2, '0');
            const secs = Math.floor(seconds % 60)
                .toString()
                .padStart(2, '0');

            return `${hrs}:${mins}:${secs}`;
        },

        escapeHtml(value) {
            if (value === null || value === undefined) {
                return '';
            }

            return String(value)
                .replace(/&/g, '&amp;')
                .replace(/</g, '&lt;')
                .replace(/>/g, '&gt;')
                .replace(/"/g, '&quot;')
                .replace(/'/g, '&#39;');
        },

        toNumber(value, fallback) {
            const parsed = Number(value);
            if (Number.isNaN(parsed)) {
                return fallback ?? 0;
            }

            return parsed;
        },

        normalizeString(value) {
            if (value === null || value === undefined) {
                return null;
            }

            const trimmed = String(value).trim();
            return trimmed.length ? trimmed : null;
        }
    };

    document.addEventListener('click', (event) => {
        const target = event.target.closest('[data-action]');
        if (!target) {
            return;
        }

        const action = target.getAttribute('data-action');
        const id = target.getAttribute('data-id');
        if (!action || !id || !window.monitoringTargets) {
            return;
        }

        switch (action) {
            case 'check':
                window.monitoringTargets.checkNow(id);
                break;
            case 'outages':
                window.monitoringTargets.showOutages(id);
                break;
            case 'edit':
                window.monitoringTargets.openEdit(id);
                break;
            case 'delete':
                window.monitoringTargets.deleteTarget(id);
                break;
            default:
                break;
        }
    });

    window.monitoringTargets = module;
})(window);
