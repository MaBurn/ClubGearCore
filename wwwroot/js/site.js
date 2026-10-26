// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your JavaScript code.

// Delegated on document because member edit forms are frequently injected via
// fetch()+innerHTML (edit modal on Members/Index), and <script> tags inside
// HTML assigned to innerHTML never execute. A document-level listener keeps
// working no matter how the #MembershipTypeId select ended up in the DOM.
document.addEventListener('change', function (event) {
    if (!event.target || event.target.id !== 'MembershipTypeId') {
        return;
    }

    var selectedId = event.target.value;
    document.querySelectorAll('.member-metadata-group').forEach(function (group) {
        group.style.display = (group.getAttribute('data-membership-type-id') === selectedId) ? '' : 'none';
    });
});

// MemberReference metadata fields: search-as-you-type instead of typing a raw member ID.
// Same delegation reasoning as above - these fields can be injected via the edit modal.
(function () {
    var searchTimers = new WeakMap();

    function findPicker(el) {
        return el.closest ? el.closest('.member-reference-picker') : null;
    }

    function renderResults(picker, searchInput, options) {
        var list = picker.querySelector('.member-reference-results');
        if (!list) {
            return;
        }

        list.innerHTML = '';
        if (!options || options.length === 0) {
            list.style.display = 'none';
            return;
        }

        options.forEach(function (option) {
            var item = document.createElement('button');
            item.type = 'button';
            item.className = 'list-group-item list-group-item-action';
            item.textContent = option.label;
            item.addEventListener('click', function () {
                var targetSelector = searchInput.getAttribute('data-value-target');
                var hidden = targetSelector ? picker.querySelector(targetSelector) : null;
                if (hidden) {
                    hidden.value = option.id;
                }

                searchInput.value = option.label;
                list.style.display = 'none';
                list.innerHTML = '';
            });
            list.appendChild(item);
        });

        list.style.display = '';
    }

    function runSearch(searchInput) {
        var picker = findPicker(searchInput);
        if (!picker) {
            return;
        }

        fetch('/api/member/reference-search?q=' + encodeURIComponent(searchInput.value.trim()), {
            headers: { 'X-Requested-With': 'XMLHttpRequest' }
        })
            .then(function (response) { return response.ok ? response.json() : []; })
            .then(function (options) { renderResults(picker, searchInput, options); })
            .catch(function () { /* transient search errors are not user-actionable here */ });
    }

    document.addEventListener('input', function (event) {
        var target = event.target;
        if (!target.classList || !target.classList.contains('member-reference-search')) {
            return;
        }

        clearTimeout(searchTimers.get(target));
        searchTimers.set(target, setTimeout(function () { runSearch(target); }, 250));
    });

    document.addEventListener('focusin', function (event) {
        var target = event.target;
        if (!target.classList || !target.classList.contains('member-reference-search')) {
            return;
        }

        runSearch(target);
    });

    document.addEventListener('focusout', function (event) {
        var target = event.target;
        if (!target.classList || !target.classList.contains('member-reference-search')) {
            return;
        }

        var picker = findPicker(target);
        if (!picker) {
            return;
        }

        // Deferred so a click on a suggestion (which blurs the input first) can still register.
        setTimeout(function () {
            var list = picker.querySelector('.member-reference-results');
            if (list) {
                list.style.display = 'none';
            }

            if (target.value.trim() === '') {
                var targetSelector = target.getAttribute('data-value-target');
                var hidden = targetSelector ? picker.querySelector(targetSelector) : null;
                if (hidden) {
                    hidden.value = '';
                }
            }
        }, 150);
    });
})();

// Generic member plugin actions. Kept in site.js because member edit forms can be
// loaded through fetch()+innerHTML; inline scripts in injected partials do not run.
(function () {
    var activeActionButton = null;

    function getModalElement() {
        return document.getElementById('member-plugin-action-modal');
    }

    function getActionEndpoint(button) {
        var buttonEndpoint = button ? button.getAttribute('data-plugin-action-endpoint') : null;
        if (buttonEndpoint) {
            return buttonEndpoint;
        }

        var modal = getModalElement();
        return modal ? (modal.getAttribute('data-plugin-action-endpoint') || '/api/member/plugin-actions') : '/api/member/plugin-actions';
    }

    function getModalPart(selector) {
        var modal = getModalElement();
        return modal ? modal.querySelector(selector) : null;
    }

    function getModalInstance() {
        var modal = getModalElement();
        if (!modal || typeof bootstrap === 'undefined') {
            return null;
        }

        return bootstrap.Modal.getOrCreateInstance(modal);
    }

    function escapeHtml(value) {
        return String(value ?? '')
            .replace(/&/g, '&amp;')
            .replace(/</g, '&lt;')
            .replace(/>/g, '&gt;')
            .replace(/"/g, '&quot;')
            .replace(/'/g, '&#39;');
    }

    function selectorValue(value) {
        return String(value ?? '').replace(/\\/g, '\\\\').replace(/"/g, '\\"');
    }

    function hasValue(value) {
        return value !== null && value !== undefined && value !== '';
    }

    function parseActionSchema(button) {
        try {
            return JSON.parse(button.getAttribute('data-plugin-action-schema') || '[]');
        } catch {
            return [];
        }
    }

    function parseActionArguments(button) {
        try {
            var parsed = JSON.parse(button.getAttribute('data-plugin-action-arguments') || '{}');
            return parsed && typeof parsed === 'object' && !Array.isArray(parsed) ? parsed : {};
        } catch {
            return {};
        }
    }

    function buildPayload(button, schema) {
        var payload = {
            moduleId: button.getAttribute('data-plugin-module-id'),
            actionKey: button.getAttribute('data-plugin-action-key'),
            arguments: parseActionArguments(button)
        };

        var memberId = button.getAttribute('data-plugin-member-id');
        if (memberId) {
            payload.memberId = Number(memberId);
        }

        if (!schema || schema.length === 0) {
            return payload;
        }

        var fields = getModalPart('[data-plugin-action-fields]');
        if (!fields) {
            return payload;
        }

        schema.forEach(function (field) {
            var control = fields.querySelector('[data-plugin-action-field="' + selectorValue(field.key) + '"]');
            if (!control) {
                return;
            }

            payload.arguments[field.key] = field.inputType === 'Boolean'
                ? (control.checked ? 'true' : 'false')
                : control.value;
        });

        return payload;
    }

    function clearModalErrors() {
        var summary = getModalPart('[data-plugin-action-summary]');
        var fields = getModalPart('[data-plugin-action-fields]');

        if (summary) {
            summary.textContent = '';
            summary.classList.add('d-none');
        }

        if (!fields) {
            return;
        }

        fields.querySelectorAll('[data-plugin-action-feedback]').forEach(function (feedback) {
            feedback.textContent = '';
        });

        fields.querySelectorAll('.is-invalid').forEach(function (control) {
            control.classList.remove('is-invalid');
        });
    }

    function applyFieldErrors(fieldErrors) {
        var fields = getModalPart('[data-plugin-action-fields]');
        if (!fields) {
            return;
        }

        var groupedErrors = new Map();
        (fieldErrors || []).forEach(function (error) {
            var key = String(error.fieldKey || '');
            var messages = groupedErrors.get(key) || [];
            messages.push(error.message);
            groupedErrors.set(key, messages);
        });

        groupedErrors.forEach(function (messages, fieldKey) {
            var control = fields.querySelector('[data-plugin-action-field="' + selectorValue(fieldKey) + '"]');
            if (!control) {
                return;
            }

            control.classList.add('is-invalid');
            var feedback = fields.querySelector('[data-plugin-action-feedback="' + selectorValue(fieldKey) + '"]');
            if (feedback) {
                feedback.textContent = messages.join(' ');
            }
        });
    }

    function renderField(field) {
        var wrapperClass = field.inputType === 'MultilineText' ? 'col-12' : 'col-12 col-md-6';
        var fieldId = 'plugin-action-' + field.key;
        var constraints = field.constraints || {};
        var requiredAttr = field.required ? ' required' : '';
        var placeholderAttr = hasValue(field.placeholder) ? ' placeholder="' + escapeHtml(field.placeholder) + '"' : '';
        var defaultValueAttr = hasValue(field.defaultValue) ? ' value="' + escapeHtml(field.defaultValue) + '"' : '';
        var helpText = hasValue(field.helpText) ? '<div class="form-text">' + escapeHtml(field.helpText) + '</div>' : '';
        var label = escapeHtml(field.label) + (field.required ? ' <span class="text-danger">*</span>' : '');
        var minLengthAttr = hasValue(constraints.minLength) ? ' minlength="' + constraints.minLength + '"' : '';
        var maxLengthAttr = hasValue(constraints.maxLength) ? ' maxlength="' + constraints.maxLength + '"' : '';
        var minAttr = hasValue(constraints.min) ? ' min="' + constraints.min + '"' : '';
        var maxAttr = hasValue(constraints.max) ? ' max="' + constraints.max + '"' : '';
        var regexAttr = hasValue(constraints.regexPattern) ? ' pattern="' + escapeHtml(constraints.regexPattern) + '"' : '';
        var dateMinAttr = hasValue(constraints.dateMin) ? ' min="' + escapeHtml(constraints.dateMin) + '"' : '';
        var dateMaxAttr = hasValue(constraints.dateMax) ? ' max="' + escapeHtml(constraints.dateMax) + '"' : '';

        if (field.inputType === 'Boolean') {
            return '<div class="' + wrapperClass + '">'
                + '<div class="form-check mt-2">'
                + '<input class="form-check-input" type="checkbox" id="' + fieldId + '" data-plugin-action-field="' + escapeHtml(field.key) + '"' + requiredAttr + ' />'
                + '<label class="form-check-label" for="' + fieldId + '">' + label + '</label>'
                + '</div>' + helpText
                + '<div class="invalid-feedback d-block" data-plugin-action-feedback="' + escapeHtml(field.key) + '"></div>'
                + '</div>';
        }

        if (field.inputType === 'MultilineText') {
            return '<div class="' + wrapperClass + '">'
                + '<label class="form-label" for="' + fieldId + '">' + label + '</label>'
                + '<textarea class="form-control" id="' + fieldId + '" rows="4" data-plugin-action-field="' + escapeHtml(field.key) + '"' + requiredAttr + placeholderAttr + minLengthAttr + maxLengthAttr + regexAttr + '></textarea>'
                + helpText
                + '<div class="invalid-feedback" data-plugin-action-feedback="' + escapeHtml(field.key) + '"></div>'
                + '</div>';
        }

        if (field.inputType === 'Number') {
            return '<div class="' + wrapperClass + '">'
                + '<label class="form-label" for="' + fieldId + '">' + label + '</label>'
                + '<input class="form-control" type="number" step="any" id="' + fieldId + '" data-plugin-action-field="' + escapeHtml(field.key) + '"' + requiredAttr + placeholderAttr + defaultValueAttr + minAttr + maxAttr + ' />'
                + helpText
                + '<div class="invalid-feedback" data-plugin-action-feedback="' + escapeHtml(field.key) + '"></div>'
                + '</div>';
        }

        if (field.inputType === 'Date') {
            return '<div class="' + wrapperClass + '">'
                + '<label class="form-label" for="' + fieldId + '">' + label + '</label>'
                + '<input class="form-control" type="date" id="' + fieldId + '" data-plugin-action-field="' + escapeHtml(field.key) + '"' + requiredAttr + defaultValueAttr + dateMinAttr + dateMaxAttr + ' />'
                + helpText
                + '<div class="invalid-feedback" data-plugin-action-feedback="' + escapeHtml(field.key) + '"></div>'
                + '</div>';
        }

        if (field.inputType === 'Combo') {
            var listId = 'dl-' + field.key;
            var comboOptions = Array.isArray(field.options)
                ? field.options.map(function (option) {
                    return '<option value="' + escapeHtml(option.value) + '">' + escapeHtml(option.label) + '</option>';
                }).join('')
                : '';

            return '<div class="' + wrapperClass + '">'
                + '<label class="form-label" for="' + fieldId + '">' + label + '</label>'
                + '<input class="form-control" type="text" list="' + escapeHtml(listId) + '" id="' + fieldId + '" data-plugin-action-field="' + escapeHtml(field.key) + '"' + requiredAttr + placeholderAttr + defaultValueAttr + minLengthAttr + maxLengthAttr + regexAttr + ' />'
                + '<datalist id="' + escapeHtml(listId) + '">' + comboOptions + '</datalist>'
                + helpText
                + '<div class="invalid-feedback" data-plugin-action-feedback="' + escapeHtml(field.key) + '"></div>'
                + '</div>';
        }

        if (field.inputType === 'Select') {
            var options = Array.isArray(field.options)
                ? field.options.map(function (option) {
                    return '<option value="' + escapeHtml(option.value) + '">' + escapeHtml(option.label) + '</option>';
                }).join('')
                : '';

            return '<div class="' + wrapperClass + '">'
                + '<label class="form-label" for="' + fieldId + '">' + label + '</label>'
                + '<select class="form-select" id="' + fieldId + '" data-plugin-action-field="' + escapeHtml(field.key) + '"' + requiredAttr + '>'
                + '<option value="">Bitte waehlen</option>' + options
                + '</select>' + helpText
                + '<div class="invalid-feedback" data-plugin-action-feedback="' + escapeHtml(field.key) + '"></div>'
                + '</div>';
        }

        return '<div class="' + wrapperClass + '">'
            + '<label class="form-label" for="' + fieldId + '">' + label + '</label>'
            + '<input class="form-control" type="text" id="' + fieldId + '" data-plugin-action-field="' + escapeHtml(field.key) + '"' + requiredAttr + placeholderAttr + defaultValueAttr + minLengthAttr + maxLengthAttr + regexAttr + ' />'
            + helpText
            + '<div class="invalid-feedback" data-plugin-action-feedback="' + escapeHtml(field.key) + '"></div>'
            + '</div>';
    }

    function applyPresetArguments(fields, presetArguments) {
        if (!fields || !presetArguments) {
            return;
        }

        Object.keys(presetArguments).forEach(function (key) {
            var control = fields.querySelector('[data-plugin-action-field="' + selectorValue(key) + '"]');
            if (!control) {
                return;
            }

            if (control.type === 'checkbox') {
                control.checked = String(presetArguments[key]).toLowerCase() === 'true';
                return;
            }

            control.value = presetArguments[key] ?? '';
        });
    }

    function showSchemaAction(button, schema) {
        var modal = getModalElement();
        var form = getModalPart('[data-plugin-action-form]');
        var fields = getModalPart('[data-plugin-action-fields]');
        var title = getModalPart('[data-plugin-action-modal-title]');
        var meta = getModalPart('[data-plugin-action-modal-meta]');
        var submit = getModalPart('[data-plugin-action-submit]');

        if (!modal || !form || !fields || !title || !meta || !submit) {
            window.alert('Plugin-Aktion kann nicht geoeffnet werden.');
            return;
        }

        activeActionButton = button;
        clearModalErrors();
        fields.innerHTML = schema.map(renderField).join('');
        applyPresetArguments(fields, parseActionArguments(button));
        title.textContent = button.getAttribute('data-plugin-action-label') || 'Plugin-Aktion';
        meta.textContent = button.getAttribute('data-plugin-action-confirm') || '';
        submit.textContent = button.getAttribute('data-plugin-action-label') || 'Ausfuehren';
        submit.className = button.className;
        getModalInstance()?.show();
    }

    async function executeAction(button, schema) {
        var endpoint = getActionEndpoint(button);
        var payload = buildPayload(button, schema);

        button.disabled = true;
        try {
            var response = await fetch(endpoint, {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify(payload)
            });

            var result = await response.json().catch(function () { return {}; });
            if (!response.ok || result.success === false) {
                return { success: false, result: result };
            }

            return { success: true, result: result };
        } catch {
            return { success: false, result: { message: 'Plugin-Aktion fehlgeschlagen.' } };
        } finally {
            button.disabled = false;
        }
    }

    document.addEventListener('click', function (event) {
        var button = event.target.closest ? event.target.closest('[data-plugin-member-action]') : null;
        if (!button) {
            return;
        }

        var schema = parseActionSchema(button);
        if (schema.length > 0) {
            showSchemaAction(button, schema);
            return;
        }

        var confirmMessage = button.getAttribute('data-plugin-action-confirm');
        if (confirmMessage && !window.confirm(confirmMessage)) {
            return;
        }

        executeAction(button, []).then(function (outcome) {
            if (!outcome.success) {
                window.alert(outcome.result.message || 'Plugin-Aktion fehlgeschlagen.');
                return;
            }

            window.location.reload();
        });
    });

    document.addEventListener('submit', function (event) {
        var form = event.target;
        if (!form.matches || !form.matches('[data-plugin-action-form]')) {
            return;
        }

        event.preventDefault();
        if (!activeActionButton) {
            return;
        }

        if (!form.checkValidity()) {
            form.reportValidity();
            return;
        }

        clearModalErrors();
        var schema = parseActionSchema(activeActionButton);
        var submit = getModalPart('[data-plugin-action-submit]');
        if (submit) {
            submit.disabled = true;
        }

        executeAction(activeActionButton, schema).then(function (outcome) {
            if (!outcome.success) {
                if (outcome.result.fieldErrors && outcome.result.fieldErrors.length > 0) {
                    applyFieldErrors(outcome.result.fieldErrors);
                }

                var summary = getModalPart('[data-plugin-action-summary]');
                if (summary) {
                    summary.textContent = outcome.result.message || 'Plugin-Aktion fehlgeschlagen.';
                    summary.classList.remove('d-none');
                } else {
                    window.alert(outcome.result.message || 'Plugin-Aktion fehlgeschlagen.');
                }
                return;
            }

            getModalInstance()?.hide();
            window.location.reload();
        }).finally(function () {
            if (submit) {
                submit.disabled = false;
            }
        });
    });
})();
