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

    const defaultPageSize = 12;

    const module = {
        init(options) {
            this.container = options.container;
            this.filterSelect = options.filterSelect;
            this.searchInput = options.searchInput;
            this.sortSelect = options.sortSelect;
            this.checkAllButton = options.checkAllButton;
            this.newButton = options.newButton;
            this.outagesModalEl = options.outagesModal;
            this.outagesModalTitle = options.outagesModalTitle;
            this.outagesTableBody = options.outagesTableBody;
            this.manageModalEl = options.manageModal;
            this.manageForm = options.manageForm;
            this.manageErrors = options.manageErrors;
            this.manageSubmitButton = options.manageSubmitButton;
            this.alertPolicyModalEl = options.alertPolicyModal;
            this.alertPolicyForm = options.alertPolicyForm;
            this.alertPolicyErrors = options.alertPolicyErrors;
            this.alertPolicySubmit = options.alertPolicySubmit;
            this.alertPolicyInheritance = options.alertPolicyInheritance;
            this.maintenanceButton = options.maintenanceButton;
            this.maintenanceModalEl = options.maintenanceModal;
            this.maintenanceForm = options.maintenanceForm;
            this.maintenanceErrors = options.maintenanceErrors;
            this.maintenanceSubmit = options.maintenanceSubmit;
            this.maintenanceTableBody = options.maintenanceTableBody;
            this.maintenanceTargetSelect = options.maintenanceTargetSelect;
            this.pageInfo = options.pageInfo;
            this.prevButton = options.prevButton;
            this.nextButton = options.nextButton;
            this.permissions = window.monitoringPermissions || { canRun: false, canEdit: false, canDelete: false, canCreate: false };

            this.pulseTimers = [];
            this.targetCache = new Map();
            this.relativeTimeFormatter = new Intl.RelativeTimeFormat(undefined, { numeric: 'auto' });
            this.state = this.readInitialState();
            this.searchDebounceHandle = null;
            this.formMode = 'create';
            this.editingTargetId = null;
            this.alertPolicyTargetId = null;
            this.maintenanceFilterTargetId = null;

            if (this.outagesModalEl) {
                this.outagesModal = new bootstrap.Modal(this.outagesModalEl);
            }

            if (this.manageModalEl) {
                this.manageModal = new bootstrap.Modal(this.manageModalEl);
                this.manageModalEl.addEventListener('hidden.bs.modal', () => this.resetForm());
            }

            if (this.alertPolicyModalEl) {
                this.alertPolicyModal = new bootstrap.Modal(this.alertPolicyModalEl);
                this.alertPolicyModalEl.addEventListener('hidden.bs.modal', () => this.resetAlertPolicyForm());
            }

            if (this.maintenanceModalEl) {
                this.maintenanceModal = new bootstrap.Modal(this.maintenanceModalEl);
                this.maintenanceModalEl.addEventListener('hidden.bs.modal', () => this.resetMaintenanceForm());
            }

            this.applyStateToControls();
            this.bindEvents();
            this.refresh(true);
        },

        readInitialState() {
            const params = new URLSearchParams(window.location.search);
            const type = params.get('type') || 'all';
            const search = params.get('search') || '';
            const sorting = params.get('sorting') || 'Name';
            const skipCount = Math.max(Number.parseInt(params.get('skipCount') || '0', 10) || 0, 0);
            const maxResultCount = Math.max(Number.parseInt(params.get('maxResultCount') || defaultPageSize.toString(), 10) || defaultPageSize, 1);

            return {
                type,
                search,
                sorting,
                skipCount,
                maxResultCount: Math.min(maxResultCount, 100)
            };
        },

        applyStateToControls() {
            if (this.filterSelect) {
                this.filterSelect.value = this.state.type || 'all';
            }

            if (this.searchInput) {
                this.searchInput.value = this.state.search;
            }

            if (this.sortSelect) {
                this.sortSelect.value = this.state.sorting;
            }
        },

        bindEvents() {
            if (this.filterSelect) {
                this.filterSelect.addEventListener('change', () => {
                    this.state.type = this.filterSelect.value || 'all';
                    this.state.skipCount = 0;
                    this.refresh();
                });
            }

            if (this.searchInput) {
                this.searchInput.addEventListener('input', () => {
                    clearTimeout(this.searchDebounceHandle);
                    this.searchDebounceHandle = setTimeout(() => {
                        this.state.search = (this.searchInput.value || '').trim();
                        this.state.skipCount = 0;
                        this.refresh();
                    }, 300);
                });
            }

            if (this.sortSelect) {
                this.sortSelect.addEventListener('change', () => {
                    this.state.sorting = this.sortSelect.value || 'Name';
                    this.state.skipCount = 0;
                    this.refresh();
                });
            }

            if (this.checkAllButton) {
                if (!this.permissions.canRun) {
                    this.checkAllButton.classList.add('disabled');
                    this.checkAllButton.setAttribute('disabled', 'disabled');
                } else {
                    this.checkAllButton.addEventListener('click', (event) => {
                        event.preventDefault();
                        this.checkAllNow();
                    });
                }
            }

            if (this.newButton) {
                if (!this.permissions.canCreate) {
                    this.newButton.classList.add('disabled');
                    this.newButton.setAttribute('disabled', 'disabled');
                } else {
                    this.newButton.addEventListener('click', (event) => {
                        event.preventDefault();
                        this.openCreate();
                    });
                }
            }

            if (this.manageForm) {
                this.manageForm.addEventListener('submit', (event) => this.submitManageForm(event));
            }

            if (this.alertPolicyForm) {
                this.alertPolicyForm.addEventListener('submit', (event) => this.submitAlertPolicy(event));
            }

            if (this.maintenanceButton) {
                this.maintenanceButton.addEventListener('click', (event) => {
                    event.preventDefault();
                    this.openMaintenanceModal();
                });
            }

            if (this.maintenanceForm) {
                this.maintenanceForm.addEventListener('submit', (event) => this.submitMaintenance(event));
            }

            if (this.maintenanceTableBody) {
                this.maintenanceTableBody.addEventListener('click', (event) => this.handleMaintenanceTableClick(event));
            }

            if (this.prevButton) {
                this.prevButton.addEventListener('click', () => {
                    if (this.state.skipCount <= 0) {
                        return;
                    }

                    this.state.skipCount = Math.max(0, this.state.skipCount - this.state.maxResultCount);
                    this.refresh();
                });
            }

            if (this.nextButton) {
                this.nextButton.addEventListener('click', () => {
                    const nextSkip = this.state.skipCount + this.state.maxResultCount;
                    if (nextSkip >= (this.totalCount || 0)) {
                        return;
                    }

                    this.state.skipCount = nextSkip;
                    this.refresh();
                });
            }
        },

        refresh(updateHistory = true) {
            if (!this.container) {
                return;
            }

            this.clearPulseTimers();
            this.container.innerHTML = '<div class="col-12"><div class="alert alert-info mb-0">Loading services…</div></div>';

            this.getTargets(this.buildQueryParams())
                .then((result) => {
                    this.totalCount = result.totalCount || 0;
                    const items = Array.isArray(result.items) ? result.items : [];
                    this.renderTargets(items);
                    this.renderPager();
                    if (updateHistory) {
                        this.updateHistory();
                    }
                })
                .catch((error) => this.handleError(error, 'Failed to load monitoring targets.'));
        },

        buildQueryParams() {
            const params = new URLSearchParams();
            params.set('skipCount', this.state.skipCount.toString());
            params.set('maxResultCount', this.state.maxResultCount.toString());

            if (this.state.sorting) {
                params.set('sorting', this.state.sorting);
            }

            if (this.state.type && this.state.type !== 'all') {
                params.set('type', this.state.type);
            }

            if (this.state.search) {
                params.set('search', this.state.search);
            }

            return params;
        },

        updateHistory() {
            const params = this.buildQueryParams();
            const url = `${window.location.pathname}?${params.toString()}`;
            window.history.replaceState({}, '', url);
        },

        renderTargets(targets) {
            this.targetCache.clear();

            if (!targets.length) {
                this.container.innerHTML = '<div class="col-12"><div class="alert alert-warning mb-0">No monitoring services matched your filters.</div></div>';
                return;
            }

            const cards = targets.map((target) => {
                this.targetCache.set(target.id, target);
                return this.renderCard(target);
            });

            this.container.innerHTML = cards.join('');

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
            const maintenanceIndicator = target.hasActiveMaintenance
                ? '<span class="badge bg-warning text-dark ms-2" title="Under maintenance"><i class="fa fa-tools me-1"></i>Maintenance</span>'
                : '';

            const actions = [];

            if (this.permissions.canRun) {
                actions.push(`<button type="button" class="btn btn-outline-primary btn-sm" data-action="check" data-id="${target.id}" aria-label="Check ${this.escapeHtml(target.name)} now"><i class="fa fa-sync me-1" aria-hidden="true"></i>Check Now</button>`);
            }

            actions.push(`<button type="button" class="btn btn-outline-secondary btn-sm" data-action="outages" data-id="${target.id}" aria-label="View outages for ${this.escapeHtml(target.name)}"><i class="fa fa-bolt me-1" aria-hidden="true"></i>View outages</button>`);

            if (this.permissions.canEdit) {
                actions.push(`<button type="button" class="btn btn-outline-warning btn-sm" data-action="policy" data-id="${target.id}" aria-label="Configure alert policy for ${this.escapeHtml(target.name)}"><i class="fa fa-bell me-1" aria-hidden="true"></i>Alert Policy</button>`);
                actions.push(`<button type="button" class="btn btn-outline-info btn-sm" data-action="edit" data-id="${target.id}" aria-label="Edit ${this.escapeHtml(target.name)}"><i class="fa fa-pen me-1" aria-hidden="true"></i>Edit</button>`);
            }

            if (this.permissions.canDelete) {
                actions.push(`<button type="button" class="btn btn-outline-danger btn-sm" data-action="delete" data-id="${target.id}" aria-label="Delete ${this.escapeHtml(target.name)}"><i class="fa fa-trash me-1" aria-hidden="true"></i>Delete</button>`);
            }

            const escapedName = this.escapeHtml(target.name);
            const escapedEndpoint = this.escapeHtml(target.endpoint);

            return `
                <div class="col-12 col-md-6 col-xl-4">
                    <div class="status-border ${statusClass} h-100 p-3 bg-white shadow-sm" id="monitoring-card-${target.id}" data-target-id="${target.id}">
                        <div class="d-flex justify-content-between align-items-start">
                            <div>
                                <h5 class="mb-1">${escapedName}</h5>
                                <span class="badge bg-light text-dark">${typeLabel}</span>${maintenanceIndicator}
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

        renderPager() {
            if (!this.pageInfo || !this.prevButton || !this.nextButton) {
                return;
            }

            const total = this.totalCount || 0;
            if (total === 0) {
                this.pageInfo.textContent = 'No services found';
                this.prevButton.setAttribute('disabled', 'disabled');
                this.nextButton.setAttribute('disabled', 'disabled');
                return;
            }

            const pageSize = this.state.maxResultCount;
            const currentPage = Math.floor(this.state.skipCount / pageSize) + 1;
            const totalPages = Math.max(1, Math.ceil(total / pageSize));
            const start = this.state.skipCount + 1;
            const end = Math.min(total, this.state.skipCount + pageSize);

            this.pageInfo.textContent = `Showing ${start}–${end} of ${total}`;

            if (currentPage <= 1) {
                this.prevButton.setAttribute('disabled', 'disabled');
            } else {
                this.prevButton.removeAttribute('disabled');
            }

            if (currentPage >= totalPages) {
                this.nextButton.setAttribute('disabled', 'disabled');
            } else {
                this.nextButton.removeAttribute('disabled');
            }
        },

        openCreate() {
            if (!this.permissions.canCreate || !this.manageModal) {
                return;
            }

            this.formMode = 'create';
            this.editingTargetId = null;
            this.resetForm();
            this.setFormDefaults();
            this.setFormTitle('New service');
            this.setSubmitLabel('Create');
            this.manageModal.show();
        },

        openEdit(id) {
            if (!this.permissions.canEdit || !this.manageModal) {
                return;
            }

            const target = this.targetCache.get(id);
            if (!target) {
                this.handleError(null, 'Unable to load the selected service. Please refresh and try again.');
                return;
            }

            this.formMode = 'edit';
            this.editingTargetId = id;
            this.resetForm();
            this.populateForm(target);
            this.setFormTitle(`Edit ${target.name}`);
            this.setSubmitLabel('Save changes');
            this.manageModal.show();
        },

        setFormDefaults() {
            if (!this.manageForm) {
                return;
            }

            this.manageForm.reset();
            this.manageForm.querySelector('#monitoring-manage-type').value = '0';
            this.manageForm.querySelector('#monitoring-manage-interval').value = 300;
            this.manageForm.querySelector('#monitoring-manage-timeout').value = 30;
            this.manageForm.querySelector('#monitoring-manage-retries').value = 3;
            this.manageForm.querySelector('#monitoring-manage-retry-delay').value = 30;
            this.manageForm.querySelector('#monitoring-manage-is-active').checked = true;
        },

        populateForm(target) {
            if (!this.manageForm) {
                return;
            }

            this.manageForm.querySelector('#monitoring-manage-name').value = target.name ?? '';
            this.manageForm.querySelector('#monitoring-manage-type').value = this.resolveTypeValue(target.type);
            this.manageForm.querySelector('#monitoring-manage-endpoint').value = target.endpoint ?? '';
            this.manageForm.querySelector('#monitoring-manage-settings').value = target.settingsJson ?? '';
            this.manageForm.querySelector('#monitoring-manage-interval').value = target.checkIntervalSeconds ?? 300;
            this.manageForm.querySelector('#monitoring-manage-timeout').value = target.timeoutSeconds ?? 30;
            this.manageForm.querySelector('#monitoring-manage-retries').value = target.maxRetryAttempts ?? 0;
            this.manageForm.querySelector('#monitoring-manage-retry-delay').value = target.retryDelaySeconds ?? 1;
            this.manageForm.querySelector('#monitoring-manage-category').value = target.category ?? '';
            this.manageForm.querySelector('#monitoring-manage-is-active').checked = Boolean(target.isActive);
        },

        resetForm() {
            if (this.manageErrors) {
                this.manageErrors.classList.add('d-none');
                this.manageErrors.textContent = '';
            }

            if (this.manageForm) {
                this.manageForm.reset();
            }
        },

        resetAlertPolicyForm() {
            this.alertPolicyTargetId = null;
            if (this.alertPolicyErrors) {
                this.alertPolicyErrors.classList.add('d-none');
                this.alertPolicyErrors.textContent = '';
            }

            if (this.alertPolicyForm) {
                this.alertPolicyForm.reset();
            }

            if (this.alertPolicyInheritance) {
                this.alertPolicyInheritance.textContent = '';
            }

            const titleEl = document.getElementById('monitoring-alert-policy-modal-title');
            if (titleEl) {
                titleEl.textContent = 'Alert policy';
            }
        },

        showAlertPolicyError(message) {
            if (!this.alertPolicyErrors) {
                return;
            }

            if (message) {
                this.alertPolicyErrors.classList.remove('d-none');
                this.alertPolicyErrors.textContent = message;
            } else {
                this.alertPolicyErrors.classList.add('d-none');
                this.alertPolicyErrors.textContent = '';
            }
        },

        openAlertPolicy(id) {
            if (!this.permissions.canEdit || !this.alertPolicyModal || !this.alertPolicyForm) {
                return;
            }

            const target = this.targetCache.get(id);
            if (!target) {
                this.handleError(null, 'Unable to load the selected service. Please refresh and try again.');
                return;
            }

            this.resetAlertPolicyForm();
            this.alertPolicyTargetId = id;

            const titleEl = document.getElementById('monitoring-alert-policy-modal-title');
            if (titleEl) {
                titleEl.textContent = `Alert policy · ${this.escapeHtml(target.name ?? '')}`;
            }

            this.toggleButtonState(this.alertPolicySubmit, true);
            this.request(`/api/monitoring/targets/${id}/alert-policy`, { method: 'GET' })
                .then((policy) => {
                    this.populateAlertPolicyForm(policy);
                    this.alertPolicyModal.show();
                })
                .catch((error) => this.showAlertPolicyError(this.extractErrorMessage(error) || 'Failed to load alert policy.'))
                .finally(() => this.toggleButtonState(this.alertPolicySubmit, false));
        },

        populateAlertPolicyForm(policy) {
            if (!this.alertPolicyForm) {
                return;
            }

            const form = this.alertPolicyForm;
            form.querySelector('#monitoring-alert-policy-target-id').value = policy?.targetId ?? '';
            form.querySelector('#monitoring-alert-policy-enabled').checked = Boolean(policy?.enabled);
            form.querySelector('#monitoring-alert-policy-notify-after').value = policy?.notifyAfterFailures ?? 1;
            form.querySelector('#monitoring-alert-policy-repeat').value = policy?.repeatMinutes ?? 60;
            form.querySelector('#monitoring-alert-policy-recover').value = policy?.recoverQuietMinutes ?? 10;
            form.querySelector('#monitoring-alert-policy-channels').value = policy?.channelsJson ?? '';
            form.querySelector('#monitoring-alert-policy-suppress').checked = Boolean(policy?.suppressDuringMaintenance);

            if (this.alertPolicyInheritance) {
                this.alertPolicyInheritance.textContent = policy?.isInherited
                    ? 'Using global defaults.'
                    : 'Custom policy for this service.';
            }
        },

        submitAlertPolicy(event) {
            event.preventDefault();
            if (!this.alertPolicyForm || !this.alertPolicyTargetId) {
                return;
            }

            const form = this.alertPolicyForm;
            this.showAlertPolicyError(null);

            const channelsValue = this.normalizeString(form.querySelector('#monitoring-alert-policy-channels').value);
            if (channelsValue && !this.validateJson(channelsValue)) {
                this.showAlertPolicyError('Channels JSON must be valid.');
                return;
            }

            const payload = {
                targetId: this.alertPolicyTargetId,
                enabled: form.querySelector('#monitoring-alert-policy-enabled').checked,
                notifyAfterFailures: this.toNumber(form.querySelector('#monitoring-alert-policy-notify-after').value, 1),
                repeatMinutes: this.toNumber(form.querySelector('#monitoring-alert-policy-repeat').value, 60),
                recoverQuietMinutes: this.toNumber(form.querySelector('#monitoring-alert-policy-recover').value, 10),
                channelsJson: channelsValue,
                suppressDuringMaintenance: form.querySelector('#monitoring-alert-policy-suppress').checked
            };

            this.toggleButtonState(this.alertPolicySubmit, true);
            this.request(`/api/monitoring/targets/${this.alertPolicyTargetId}/alert-policy`, {
                method: 'PUT',
                body: payload
            })
                .then(() => {
                    abp.notify.success('Alert policy saved.');
                    if (this.alertPolicyModal) {
                        this.alertPolicyModal.hide();
                    }
                })
                .catch((error) => this.showAlertPolicyError(this.extractErrorMessage(error) || 'Failed to save alert policy.'))
                .finally(() => this.toggleButtonState(this.alertPolicySubmit, false));
        },

        openMaintenanceModal(targetId = null) {
            if (!this.permissions.canEdit || !this.maintenanceModal || !this.maintenanceForm) {
                return;
            }

            this.maintenanceFilterTargetId = targetId;
            this.resetMaintenanceForm();
            this.populateMaintenanceTargets(targetId);
            this.loadMaintenanceWindows(targetId);
            this.maintenanceModal.show();
        },

        populateMaintenanceTargets(selectedId) {
            if (!this.maintenanceTargetSelect) {
                return;
            }

            const options = ['<option value="">All services (global)</option>'];
            const entries = Array.from(this.targetCache.values()).sort((a, b) => (a.name || '').localeCompare(b.name || ''));

            entries.forEach((target) => {
                const selected = selectedId && target.id === selectedId ? ' selected' : '';
                options.push(`<option value="${target.id}"${selected}>${this.escapeHtml(target.name ?? target.id)}</option>`);
            });

            this.maintenanceTargetSelect.innerHTML = options.join('');

            if (selectedId) {
                this.maintenanceTargetSelect.value = selectedId;
            } else {
                this.maintenanceTargetSelect.value = '';
            }
        },

        resetMaintenanceForm() {
            if (this.maintenanceErrors) {
                this.maintenanceErrors.classList.add('d-none');
                this.maintenanceErrors.textContent = '';
            }

            if (this.maintenanceForm) {
                this.maintenanceForm.reset();
            }
        },

        showMaintenanceError(message) {
            if (!this.maintenanceErrors) {
                return;
            }

            if (message) {
                this.maintenanceErrors.classList.remove('d-none');
                this.maintenanceErrors.textContent = message;
            } else {
                this.maintenanceErrors.classList.add('d-none');
                this.maintenanceErrors.textContent = '';
            }
        },

        loadMaintenanceWindows(targetId) {
            const query = targetId ? `?targetId=${encodeURIComponent(targetId)}` : '';
            this.request(`/api/monitoring/maintenance${query}`, { method: 'GET' })
                .then((windows) => this.renderMaintenanceTable(Array.isArray(windows) ? windows : []))
                .catch((error) => this.showMaintenanceError(this.extractErrorMessage(error) || 'Failed to load maintenance windows.'));
        },

        renderMaintenanceTable(windows) {
            if (!this.maintenanceTableBody) {
                return;
            }

            if (!windows.length) {
                this.maintenanceTableBody.innerHTML = '<tr><td colspan="5" class="text-center text-muted">No maintenance windows scheduled.</td></tr>';
                return;
            }

            const rows = windows.map((window) => {
                const started = this.formatDateTime(window.startUtc);
                const ended = this.formatDateTime(window.endUtc);
                const targetName = window.targetId
                    ? (this.targetCache.get(window.targetId)?.name ?? `Target ${window.targetId}`)
                    : 'All services';
                const reason = this.escapeHtml(window.reason ?? '—');
                const deleteButton = this.permissions.canEdit
                    ? `<button type="button" class="btn btn-link btn-sm text-danger" data-maintenance-action="delete" data-id="${window.id}"><i class="fa fa-trash me-1" aria-hidden="true"></i>Delete</button>`
                    : '';

                return `<tr>
                    <td>${started}</td>
                    <td>${ended}</td>
                    <td>${this.escapeHtml(targetName)}</td>
                    <td>${reason || '—'}</td>
                    <td class="text-end">${deleteButton}</td>
                </tr>`;
            });

            this.maintenanceTableBody.innerHTML = rows.join('');
        },

        submitMaintenance(event) {
            event.preventDefault();
            if (!this.maintenanceForm) {
                return;
            }

            this.showMaintenanceError(null);

            const targetIdValue = this.normalizeString(this.maintenanceTargetSelect?.value);
            const startValue = this.normalizeString(this.maintenanceForm.querySelector('#monitoring-maintenance-start').value);
            const endValue = this.normalizeString(this.maintenanceForm.querySelector('#monitoring-maintenance-end').value);
            const reasonValue = this.normalizeString(this.maintenanceForm.querySelector('#monitoring-maintenance-reason').value);

            if (!startValue || !endValue) {
                this.showMaintenanceError('Start and end times are required.');
                return;
            }

            const payload = {
                targetId: targetIdValue || null,
                startUtc: this.toIsoUtcString(startValue),
                endUtc: this.toIsoUtcString(endValue),
                reason: reasonValue
            };

            this.toggleButtonState(this.maintenanceSubmit, true);
            this.request('/api/monitoring/maintenance', {
                method: 'POST',
                body: payload
            })
                .then(() => {
                    abp.notify.success('Maintenance window scheduled.');
                    this.resetMaintenanceForm();
                    this.populateMaintenanceTargets(this.maintenanceFilterTargetId);
                    this.loadMaintenanceWindows(this.maintenanceFilterTargetId);
                    this.refresh(false);
                })
                .catch((error) => this.showMaintenanceError(this.extractErrorMessage(error) || 'Failed to create maintenance window.'))
                .finally(() => this.toggleButtonState(this.maintenanceSubmit, false));
        },

        handleMaintenanceTableClick(event) {
            const actionEl = event.target.closest('[data-maintenance-action]');
            if (!actionEl) {
                return;
            }

            const action = actionEl.getAttribute('data-maintenance-action');
            const id = actionEl.getAttribute('data-id');
            if (!action || !id) {
                return;
            }

            if (action === 'delete') {
                this.deleteMaintenance(id);
            }
        },

        deleteMaintenance(id) {
            if (!this.permissions.canEdit) {
                return;
            }

            abp.message.confirm('Delete this maintenance window?')
                .then((confirmed) => {
                    if (!confirmed) {
                        return;
                    }

                    this.request(`/api/monitoring/maintenance/${id}`, { method: 'DELETE' })
                        .then(() => {
                            abp.notify.info('Maintenance window removed.');
                            this.loadMaintenanceWindows(this.maintenanceFilterTargetId);
                            this.refresh(false);
                        })
                        .catch((error) => this.showMaintenanceError(this.extractErrorMessage(error) || 'Failed to delete maintenance window.'));
                });
        },

        setFormTitle(title) {
            const heading = document.getElementById('monitoring-manage-modal-title');
            if (heading) {
                heading.textContent = title;
            }
        },

        setSubmitLabel(label) {
            if (!this.manageSubmitButton) {
                return;
            }

            const labelSpan = this.manageSubmitButton.querySelector('[data-role="submit-label"]');
            if (labelSpan) {
                labelSpan.textContent = label;
            }
        },

        submitManageForm(event) {
            event.preventDefault();

            if (!this.manageForm || !this.manageSubmitButton) {
                return;
            }

            const formData = new FormData(this.manageForm);
            const settings = (formData.get('settingsJson') || '').toString().trim();
            if (!this.validateJson(settings)) {
                this.showFormError('Settings JSON is invalid.');
                return;
            }

            const typeValue = formData.get('type')?.toString() || '0';

            const payload = {
                name: formData.get('name')?.toString().trim() || '',
                type: this.toNumber(typeValue, 0),
                endpoint: formData.get('endpoint')?.toString().trim() || '',
                settingsJson: settings.length ? settings : null,
                checkIntervalSeconds: this.toNumber(formData.get('checkIntervalSeconds'), 300),
                timeoutSeconds: this.toNumber(formData.get('timeoutSeconds'), 30),
                maxRetryAttempts: this.toNumber(formData.get('maxRetryAttempts'), 0),
                retryDelaySeconds: this.toNumber(formData.get('retryDelaySeconds'), 1),
                category: this.normalizeString(formData.get('category')),
                isActive: formData.get('isActive') === 'on'
            };

            if (!payload.name || payload.name.length < 2) {
                this.showFormError('Name must be at least 2 characters.');
                return;
            }

            if (!payload.endpoint) {
                this.showFormError('Endpoint is required.');
                return;
            }

            this.showFormError(null);
            this.toggleButtonState(this.manageSubmitButton, true);

            const request = this.formMode === 'edit' && this.editingTargetId
                ? this.updateTarget(this.editingTargetId, payload)
                : this.createTarget(payload);

            request
                .then(() => {
                    const message = this.formMode === 'edit' ? 'Monitoring service updated.' : 'Monitoring service created.';
                    abp.notify.success(message);
                    this.manageModal.hide();
                    if (this.formMode !== 'edit') {
                        this.state.skipCount = 0;
                    }
                    this.refresh();
                })
                .catch((error) => this.handleError(error, 'Failed to save monitoring service.', true))
                .finally(() => this.toggleButtonState(this.manageSubmitButton, false));
        },

        showFormError(message) {
            if (!this.manageErrors) {
                return;
            }

            if (!message) {
                this.manageErrors.classList.add('d-none');
                this.manageErrors.textContent = '';
                return;
            }

            this.manageErrors.textContent = message;
            this.manageErrors.classList.remove('d-none');
        },

        getTargets(params) {
            const url = `/api/monitoring/targets?${params.toString()}`;
            return this.request(url, { method: 'GET' });
        },

        createTarget(dto) {
            return this.request('/api/monitoring/targets', {
                method: 'POST',
                body: dto
            });
        },

        updateTarget(id, dto) {
            return this.request(`/api/monitoring/targets/${id}`, {
                method: 'PUT',
                body: dto
            });
        },

        deleteTarget(id) {
            if (!this.permissions.canDelete) {
                return;
            }

            abp.message
                .confirm('This will deactivate the monitoring target. Continue?', 'Delete service')
                .then((confirmed) => {
                    if (!confirmed) {
                        return;
                    }

                    this.request(`/api/monitoring/targets/${id}`, { method: 'DELETE' })
                        .then(() => {
                            abp.notify.success('Monitoring service deleted.');
                            if (this.targetCache.size <= 1 && this.state.skipCount > 0) {
                                this.state.skipCount = Math.max(0, this.state.skipCount - this.state.maxResultCount);
                            }
                            this.refresh();
                        })
                        .catch((error) => this.handleError(error, 'Failed to delete monitoring service.'));
                });
        },

        checkAllNow() {
            if (!this.permissions.canRun || !this.checkAllButton) {
                return;
            }

            this.toggleButtonState(this.checkAllButton, true);
            this.request('/api/monitoring/check-all', { method: 'POST' })
                .then((count) => {
                    abp.notify.success(`Queued ${count ?? 0} service checks.`);
                    this.refresh(false);
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
                    this.refresh(false);
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
                this.outagesTableBody.innerHTML = '<tr><td colspan="4" class="text-center text-muted">Loading…</td></tr>';
            }

            this.getOutages(id)
                .then((outages) => this.renderOutages(outages))
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

        request(url, options) {
            const init = options || {};
            init.headers = init.headers || { 'Accept': 'application/json' };

            if (init.body && typeof init.body === 'object' && !(init.body instanceof FormData)) {
                init.headers['Content-Type'] = 'application/json';
                init.body = JSON.stringify(init.body);
            }

            return fetch(url, init).then((response) => this.ensureSuccess(response));
        },

        ensureSuccess(response) {
            if (response.ok) {
                if (response.status === 204) {
                    return null;
                }

                return response.json();
            }

            return response
                .json()
                .catch(() => ({ error: { message: response.statusText } }))
                .then((payload) => Promise.reject(payload));
        },

        handleError(error, fallbackMessage, isFormError) {
            const message = this.extractErrorMessage(error) || fallbackMessage || 'An unexpected error occurred.';

            if (isFormError) {
                this.showFormError(message);
                return;
            }

            abp.notify.error(message);
        },

        extractErrorMessage(error) {
            if (!error) {
                return null;
            }

            if (typeof error === 'string') {
                return error;
            }

            const payload = error.error || error;
            if (payload) {
                if (Array.isArray(payload.validationErrors) && payload.validationErrors.length) {
                    return payload.validationErrors.map((e) => e.message || e).join('\n');
                }

                if (payload.message) {
                    return payload.message;
                }
            }

            return null;
        },

        toggleButtonState(button, isLoading) {
            if (!button) {
                return;
            }

            const spinner = button.querySelector('.spinner-border');
            const label = button.querySelector('[data-role="submit-label"]');

            if (isLoading) {
                button.setAttribute('disabled', 'disabled');
                if (spinner) {
                    spinner.classList.remove('d-none');
                }
                if (label) {
                    label.classList.add('opacity-75');
                }
            } else {
                button.removeAttribute('disabled');
                if (spinner) {
                    spinner.classList.add('d-none');
                }
                if (label) {
                    label.classList.remove('opacity-75');
                }
            }
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

        clearPulseTimers() {
            this.pulseTimers.forEach((timer) => clearInterval(timer));
            this.pulseTimers = [];
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

        resolveStatus(value) {
            if (value === null || value === undefined) {
                return 'Checking';
            }

            if (typeof value === 'string') {
                const normalized = value.trim();
                return statusLabelMap[normalized] || statusLabelMap[normalized.toLowerCase?.()] || normalized;
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

        resolveTypeValue(value) {
            if (typeof value === 'string') {
                const numeric = Number.parseInt(value, 10);
                if (!Number.isNaN(numeric)) {
                    return String(numeric);
                }

                switch (value.toLowerCase()) {
                    case 'website':
                        return '0';
                    case 'api':
                        return '1';
                    case 'tcp':
                        return '2';
                    case 'redis':
                        return '3';
                    default:
                        return '0';
                }
            }

            if (typeof value === 'number') {
                return String(value);
            }

            return '0';
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

            const hrs = Math.floor(seconds / 3600).toString().padStart(2, '0');
            const mins = Math.floor((seconds % 3600) / 60).toString().padStart(2, '0');
            const secs = Math.floor(seconds % 60).toString().padStart(2, '0');

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

            return Math.trunc(parsed);
        },

        normalizeString(value) {
            if (value === null || value === undefined) {
                return null;
            }

            const trimmed = String(value).trim();
            return trimmed.length ? trimmed : null;
        },

        toIsoUtcString(value) {
            const normalized = this.normalizeString(value);
            if (!normalized) {
                return null;
            }

            if (normalized.endsWith('Z')) {
                return normalized;
            }

            if (normalized.length === 16) { // YYYY-MM-DDTHH:mm
                return `${normalized}:00Z`;
            }

            if (normalized.length === 19) { // YYYY-MM-DDTHH:mm:ss
                return `${normalized}Z`;
            }

            return `${normalized}Z`;
        },

        validateJson(str) {
            if (!str) {
                return true;
            }

            try {
                JSON.parse(str);
                return true;
            } catch (error) {
                return false;
            }
        }
    };

    document.addEventListener('click', (event) => {
        const actionEl = event.target.closest('[data-action]');
        if (!actionEl || !window.monitoringTargets) {
            return;
        }

        const action = actionEl.getAttribute('data-action');
        const id = actionEl.getAttribute('data-id');
        if (!action || !id) {
            return;
        }

        switch (action) {
            case 'check':
                window.monitoringTargets.checkNow(id);
                break;
            case 'outages':
                window.monitoringTargets.showOutages(id);
                break;
            case 'policy':
                window.monitoringTargets.openAlertPolicy(id);
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
