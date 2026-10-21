/*
 * ExternalAuth.SSO — admin page behaviour (Configure, SSO Accounts).
 *
 * Server values reach this file through a hidden config div per page (data-* attributes:
 * URLs, labels as a JSON resource bundle), never through Razor inside a script. Handlers are
 * delegated from the page's own container with a namespace, and removed first, so a re-render
 * cannot stack them.
 *
 * nop's admin JSON is PascalCase (IdpEntityId, Success). Reads go through val(), which also
 * accepts camelCase, so a change of serializer settings cannot silently blank these screens.
 *
 * Bundled and minified by WebOptimizer: after editing, empty Presentation/Nop.Web/wwwroot/bundles.
 * `var` for closure variables (see CLAUDE.md §9.11).
 */
window.SsoAdmin = window.SsoAdmin || (function () {
    'use strict';

    // ---- Shared helpers ---------------------------------------------------------------

    function escapeHtml(s) {
        var d = document.createElement('div');
        d.textContent = s == null ? '' : String(s);
        return d.innerHTML;
    }

    /** A property by PascalCase name, falling back to camelCase. */
    function val(obj, pascalName) {
        if (!obj) return undefined;
        if (obj[pascalName] !== undefined) return obj[pascalName];
        return obj[pascalName.charAt(0).toLowerCase() + pascalName.slice(1)];
    }

    /** Data attributes and the JSON resource bundle of a config div. */
    function readConfig(el) {
        var cfg = { el: el, data: el ? el.dataset : {}, res: {} };
        if (el && el.dataset.resources) {
            try { cfg.res = JSON.parse(el.dataset.resources); } catch (e) { cfg.res = {}; }
        }
        cfg.t = function (key, fallback) { return cfg.res[key] || fallback || key; };
        return cfg;
    }

    function antiforgeryFormData() {
        var fd = new FormData();
        var token = document.querySelector('input[name="__RequestVerificationToken"]');
        if (token) fd.append('__RequestVerificationToken', token.value);
        return fd;
    }

    function postForm(url, fields) {
        var fd = antiforgeryFormData();
        Object.keys(fields || {}).forEach(function (k) { fd.append(k, fields[k]); });
        return fetch(url, { method: 'POST', body: fd, credentials: 'same-origin' })
            .then(function (r) { return r.json(); });
    }

    function checkRow(passed, label) {
        var icon = passed
            ? '<i class="fas fa-check-circle text-success"></i>'
            : '<i class="fas fa-times-circle text-danger"></i>';
        return '<li>' + icon + ' ' + escapeHtml(label) + '</li>';
    }

    /** Test-connection result (shared by Configure and SSO Accounts). */
    function renderTestResult(data, t) {
        var success = !!val(data, 'Success');
        var html = '<strong>' + (success ? '&#10003;' : '&#10007;') + '</strong> ' + escapeHtml(val(data, 'Message'));
        var details = val(data, 'Details');
        if (details) html += '<pre class="mt-2 mb-2" style="white-space:pre-wrap;">' + escapeHtml(details) + '</pre>';

        var h = val(data, 'Handshake');
        if (!h) return { success: success, html: html };

        var skipped = val(h, 'SkippedReason');
        if (skipped) {
            html += '<div class="mt-2"><em class="text-muted">' + escapeHtml(t('HandshakeSkipped')) + ': ' + escapeHtml(skipped) + '</em></div>';
            return { success: success, html: html };
        }

        // The server sends an empty handshake when it stopped before reaching it (protocol off,
        // no certificate, ...); the per-check list means something only once metadata was read.
        if (!val(h, 'MetadataFetched')) {
            var handshakeDetail = val(h, 'Detail');
            if (handshakeDetail) html += '<div class="mt-2"><em class="text-muted">' + escapeHtml(handshakeDetail) + '</em></div>';
            return { success: success, html: html };
        }

        html += '<div class="mt-3"><strong>' + escapeHtml(t('HandshakeTitle')) + ':</strong><ul class="list-unstyled mb-0 mt-1">'
            + checkRow(val(h, 'EntityIdMatches'), t('CheckEntityId'))
            + checkRow(val(h, 'SsoUrlMatches'), t('CheckSsoUrl'))
            + checkRow(val(h, 'SloUrlMatches'), t('CheckSloUrl'))
            + checkRow(val(h, 'CertificateMatches'), t('CheckCertificate'))
            + '</ul></div>';

        var thumbprints = val(h, 'SigningKeyThumbprints') || [];
        if (thumbprints.length > 0) {
            html += '<div class="mt-3"><strong>' + escapeHtml(t('MetadataThumbprints')) + ':</strong>'
                + '<ul class="small mb-0 mt-1" style="font-family: monospace;">'
                + thumbprints.map(function (tp) { return '<li>' + escapeHtml(tp) + '</li>'; }).join('')
                + '</ul></div>';
        }
        return { success: success, html: html };
    }

    function showBanner(banner, cssClass, html) {
        banner.style.display = 'block';
        banner.className = 'alert mt-3 ' + cssClass;
        banner.innerHTML = html;
    }

    // ---- Configure page -----------------------------------------------------------------

    function initConfigure(configEl) {
        var cfg = readConfig(configEl);
        if (!configEl) return;
        var $page = $(configEl).closest('form').off('.ssoConfigure');

        // Default-roles multi-select.
        var $roles = $('#DefaultRoleIds');
        if ($roles.length) {
            var hasRoles = cfg.data.hasRoles === 'true';
            $roles.select2(hasRoles
                ? { closeOnSelect: false }
                : { closeOnSelect: false, disabled: true, placeholder: cfg.t('NoRoles') });
        }

        // Copy buttons next to the read-only URLs in the "Send to IdP team" card.
        $page.on('click.ssoConfigure', '.js-sso-copy', function () {
            var field = document.getElementById(this.getAttribute('data-copy-target'));
            if (!field) return;
            field.select();
            document.execCommand('copy');
        });

        // IdP metadata import: fills the IdP fields; nothing is saved until the admin saves.
        $page.on('click.ssoConfigure', '#btnImportIdpMetadata', function () {
            var importBtn = this;
            var urlInput = document.getElementById('IdpMetadataUrlImport');
            var fileInput = document.getElementById('IdpMetadataFileImport');
            var status = document.getElementById('IdpMetadataImportStatus');
            var fd = antiforgeryFormData();

            if (fileInput.files && fileInput.files.length > 0) fd.append('file', fileInput.files[0]);
            else if (urlInput.value && urlInput.value.trim() !== '') fd.append('url', urlInput.value.trim());
            else { status.innerHTML = '<span class="text-danger">' + escapeHtml(cfg.t('ImportNoInput')) + '</span>'; return; }

            status.innerHTML = '<i class="fas fa-spinner fa-spin"></i> ' + escapeHtml(cfg.t('Importing'));
            importBtn.disabled = true;

            fetch(cfg.data.importUrl, { method: 'POST', body: fd, credentials: 'same-origin' })
                .then(function (r) { return r.json(); })
                .then(function (data) {
                    importBtn.disabled = false;
                    if (!data || !data.success) {
                        status.innerHTML = '<span class="text-danger">' + escapeHtml((data && data.error) || cfg.t('ImportError')) + '</span>';
                        return;
                    }
                    var result = data.result;
                    var fill = function (id, value) { var el = document.getElementById(id); if (el && value) el.value = value; };
                    fill('IdpEntityId', val(result, 'IdpEntityId'));
                    fill('IdpSsoUrl', val(result, 'IdpSsoUrl'));
                    fill('IdpSloUrl', val(result, 'IdpSloUrl'));
                    fill('IdpCertificateBase64', val(result, 'IdpCertificateBase64'));

                    var warnings = val(result, 'Warnings') || [];
                    status.innerHTML = '<span class="text-success"><i class="fas fa-check"></i> ' + escapeHtml(cfg.t('Imported')) + '</span>'
                        + (warnings.length ? '<div class="text-warning small mt-1">' + warnings.map(escapeHtml).join('<br>') + '</div>' : '');
                })
                .catch(function (err) {
                    importBtn.disabled = false;
                    status.innerHTML = '<span class="text-danger">' + escapeHtml(err.message || cfg.t('ImportError')) + '</span>';
                });
        });

        // Onboarding report.
        var textArea = document.getElementById('OnboardingReportText');
        function loadOnboardingReport() {
            if (!textArea) return;
            textArea.value = cfg.t('ReportLoading');
            fetch(cfg.data.onboardingReportUrl, { credentials: 'same-origin' })
                .then(function (r) { return r.text(); })
                .then(function (txt) { textArea.value = txt; })
                .catch(function (err) { textArea.value = err.message || cfg.t('ReportError'); });
        }
        $page.on('click.ssoConfigure', '#btnRefreshOnboardingReport', loadOnboardingReport);
        $page.on('click.ssoConfigure', '#btnCopyOnboardingReport', function () {
            if (!textArea) return;
            textArea.select();
            document.execCommand('copy');
        });
        loadOnboardingReport();

        // Protocol-aware visibility (SsoProtocol: Off = 0, OpenIdConnect = 1, Saml2 = 2). Off shows
        // everything, so an admin can fill both before choosing.
        var samlCardIds = ['microsoft-idp', 'microsoft-sp'];
        var oidcCardIds = ['microsoft-oidc'];
        var samlOnlyContainerIds = ['sendToIdp-saml-section'];
        var oidcOnlyContainerIds = ['sendToIdp-oidc-section'];
        var samlOnlyFieldIds = ['LoginBinding', 'LogoutBinding'];

        function setVisible(el, visible) { if (el) el.style.display = visible ? '' : 'none'; }
        function rowFor(fieldId) { var input = document.getElementById(fieldId); return input ? input.closest('.form-group') : null; }

        function applyProtocolVisibility() {
            var protocolSelect = document.getElementById('Protocol');
            if (!protocolSelect) return;
            var p = parseInt(protocolSelect.value, 10);
            var showOidc = p !== 2;
            var showSaml = p !== 1;
            oidcCardIds.forEach(function (id) { setVisible(document.getElementById(id), showOidc); });
            samlCardIds.forEach(function (id) { setVisible(document.getElementById(id), showSaml); });
            oidcOnlyContainerIds.forEach(function (id) { setVisible(document.getElementById(id), showOidc); });
            samlOnlyContainerIds.forEach(function (id) { setVisible(document.getElementById(id), showSaml); });
            samlOnlyFieldIds.forEach(function (id) { setVisible(rowFor(id), showSaml); });
        }
        $page.on('change.ssoConfigure', '#Protocol', applyProtocolVisibility);
        applyProtocolVisibility();

        // Test connection for the scope being edited.
        $page.on('click.ssoConfigure', '#btnTestSsoConnection', function () {
            var banner = document.getElementById('TestSsoConnectionResult');
            showBanner(banner, 'alert-info', '<i class="fas fa-spinner fa-spin"></i> ' + escapeHtml(cfg.t('TestRunning')));

            postForm(cfg.data.testConnectionUrl, { storeId: cfg.data.storeId })
                .then(function (data) {
                    var r = renderTestResult(data, cfg.t);
                    showBanner(banner, r.success ? 'alert-success' : 'alert-danger', r.html);
                })
                .catch(function (err) {
                    showBanner(banner, 'alert-danger', escapeHtml(err.message || cfg.t('TestFailed')));
                });
        });
    }

    // ---- SSO Accounts page --------------------------------------------------------------

    var accountsCfg = null;

    /** The SSO Accounts config, read on first use — the grid may render before the ready handler runs. */
    function getAccountsCfg() {
        if (!accountsCfg || !accountsCfg.el)
            accountsCfg = readConfig(document.querySelector('.sso-accounts-config'));
        return accountsCfg;
    }

    /** Grid renderer (RenderCustom) for the certificate-expiry column. */
    function renderCertExpiry(data, type, row) {
        var accountsCfg = getAccountsCfg();
        if (!data || data === '—') return '<span class="text-muted">—</span>';
        if (val(row, 'CertificateExpired'))
            return '<span class="badge badge-danger" title="' + escapeHtml(accountsCfg.t('CertExpired')) + '"><i class="fas fa-exclamation-triangle"></i> ' + escapeHtml(data) + '</span>';
        if (val(row, 'CertificateNearExpiry'))
            return '<span class="badge badge-warning" title="' + escapeHtml(accountsCfg.t('CertNearExpiry')) + '"><i class="fas fa-clock"></i> ' + escapeHtml(data) + '</span>';
        return '<span class="badge badge-success">' + escapeHtml(data) + '</span>';
    }

    /** Grid renderer (RenderCustom) for the actions column; the value is the store id. */
    function renderAccountActions(data) {
        var accountsCfg = getAccountsCfg();
        var storeId = parseInt(data, 10);
        var editUrl = accountsCfg.data.editUrl + '?id=' + storeId;
        return '<button type="button" class="btn btn-info btn-sm js-sso-test" data-store-id="' + storeId + '">'
            + '<i class="fas fa-bolt"></i> ' + escapeHtml(accountsCfg.t('Test')) + '</button> '
            + '<a class="btn btn-default btn-sm" href="' + escapeHtml(editUrl) + '"><i class="fas fa-edit"></i> ' + escapeHtml(accountsCfg.t('Edit')) + '</a> '
            + '<button type="button" class="btn btn-danger btn-sm js-sso-delete" data-store-id="' + storeId + '">'
            + '<i class="fas fa-trash-alt"></i> ' + escapeHtml(accountsCfg.t('Delete')) + '</button>';
    }

    function initAccounts(configEl) {
        if (!configEl) return;
        var cfg = getAccountsCfg();
        var banner = document.getElementById('ssoaccount-test-result');
        var $page = $(configEl).closest('section').off('.ssoAccounts');

        function busy(btn) { var html = btn.innerHTML; btn.disabled = true; btn.innerHTML = '<i class="fas fa-spinner fa-spin"></i>'; return function () { btn.disabled = false; btn.innerHTML = html; }; }

        $page.on('click.ssoAccounts', '.js-sso-test', function () {
            var done = busy(this);
            postForm(cfg.data.testConnectionUrl, { storeId: this.getAttribute('data-store-id') })
                .then(function (data) {
                    done();
                    var r = renderTestResult(data, cfg.t);
                    showBanner(banner, r.success ? 'alert-success' : 'alert-danger', r.html);
                })
                .catch(function (err) { done(); showBanner(banner, 'alert-danger', escapeHtml(err.message || cfg.t('TestFailed'))); });
        });

        $page.on('click.ssoAccounts', '.js-sso-delete', function () {
            if (!confirm(cfg.t('DeleteConfirm'))) return;
            var done = busy(this);
            postForm(cfg.data.deleteUrl, { storeId: this.getAttribute('data-store-id') })
                .then(function (data) {
                    done();
                    var ok = !!(data && data.success);
                    showBanner(banner, ok ? 'alert-success' : 'alert-danger',
                        escapeHtml(ok ? (data.message || cfg.t('Deleted')) : ((data && data.error) || cfg.t('DeleteError'))));
                    if (ok) $('#ssoaccounts-grid').DataTable().ajax.reload(null, false);
                })
                .catch(function (err) { done(); showBanner(banner, 'alert-danger', escapeHtml(err.message || cfg.t('DeleteError'))); });
        });
    }

    // ---- Wire-up --------------------------------------------------------------------------

    $(function () {
        initConfigure(document.querySelector('.sso-configure-config'));
        initAccounts(document.querySelector('.sso-accounts-config'));
    });

    return {
        render: {
            certExpiry: renderCertExpiry,
            accountActions: renderAccountActions
        }
    };
})();
