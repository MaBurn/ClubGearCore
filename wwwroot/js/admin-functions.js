(function () {
  function escapeHtml(value) {
    return String(value || '')
      .replace(/&/g, '&amp;')
      .replace(/</g, '&lt;')
      .replace(/>/g, '&gt;')
      .replace(/"/g, '&quot;')
      .replace(/'/g, '&#39;');
  }

  function selectorValue(value) {
    return String(value || '').replace(/\\/g, '\\\\').replace(/"/g, '\\"');
  }

  function hasValue(value) {
    return value !== null && value !== undefined && value !== '';
  }

  var tabButtons = document.querySelectorAll('#adminTabs button[data-bs-toggle="tab"]');
  tabButtons.forEach(function (button) {
    button.addEventListener('shown.bs.tab', function (event) {
      var target = event.target.getAttribute('data-bs-target');
      if (!target) {
        return;
      }

      var tabName = target.replace('#', '');
      var url = new URL(window.location.href);
      url.searchParams.set('tab', tabName);
      window.history.replaceState({}, '', url);
    });
  });

  var pluginHost = document.querySelector('[data-plugin-panel-host]');
  if (pluginHost) {
    bindPluginPanelHost(pluginHost);
  }

  document.querySelectorAll('.modal').forEach(function (modalEl) {
    modalEl.addEventListener('shown.bs.modal', function () {
      var form = modalEl.querySelector('.cfg-form');
      if (!form || form.dataset.loaded === 'true') {
        return;
      }

      var section = form.dataset.section;
      var loading = modalEl.querySelector('.cfg-loading[data-section="' + section + '"]');
      var fieldsContainer = modalEl.querySelector('.cfg-fields[data-section="' + section + '"]');
      var fields = Array.from(form.querySelectorAll('[data-key]'));

      if (fields.length === 0) {
        if (loading) loading.style.display = 'none';
        if (fieldsContainer) fieldsContainer.style.display = '';
        form.dataset.loaded = 'true';
        return;
      }

      var loaded = 0;
      fields.forEach(function (field) {
        var key = field.dataset.key;
        var sect = field.dataset.sect || '';
        var url = '/api/config/' + encodeURIComponent(key);
        if (sect) {
          url += '?section=' + encodeURIComponent(sect);
        }

        fetch(url)
          .then(function (response) {
            if (!response.ok) {
              throw new Error('Fehler beim Laden');
            }
            return response.json();
          })
          .then(function (data) {
            if (field.type === 'checkbox') {
              field.checked = String(data.value || '').toLowerCase() === 'true';
            } else {
              field.value = data.value || '';
            }
          })
          .catch(function () {
            field.value = '';
          })
          .finally(function () {
            loaded += 1;
            if (loaded >= fields.length) {
              if (loading) loading.style.display = 'none';
              if (fieldsContainer) fieldsContainer.style.display = '';
              form.dataset.loaded = 'true';
            }
          });
      });
    });
  });

  document.querySelectorAll('.cfg-form').forEach(function (form) {
    form.addEventListener('submit', function (event) {
      event.preventDefault();

      var submitButton = form.querySelector('button[type="submit"]');
      var status = form.querySelector('.cfg-status');
      var originalButtonText = submitButton ? submitButton.innerHTML : '';
      var tokenInput = document.querySelector('input[name="__RequestVerificationToken"]');
      var token = tokenInput ? tokenInput.value : '';

      if (submitButton) {
        submitButton.disabled = true;
        submitButton.innerText = 'Speichern...';
      }
      if (status) {
        status.textContent = '';
      }

      var fields = Array.from(form.querySelectorAll('[data-key]'));
      var requests = fields.map(function (field) {
        var payload = {
          name: field.dataset.key || '',
          section: field.dataset.sect || '',
          value: field.type === 'checkbox' ? (field.checked ? 'true' : 'false') : (field.value || ''),
          description: ''
        };

        return fetch('/api/config', {
          method: 'POST',
          headers: {
            'Content-Type': 'application/json',
            'RequestVerificationToken': token
          },
          body: JSON.stringify(payload)
        }).then(function (response) {
          if (!response.ok) {
            throw new Error('Speichern fehlgeschlagen');
          }
          return response.json();
        });
      });

      Promise.all(requests)
        .then(function () {
          if (status) {
            status.textContent = 'Gespeichert';
            status.classList.remove('text-danger');
            status.classList.add('text-success');
          }
          setTimeout(function () {
            var instance = bootstrap.Modal.getInstance(form.closest('.modal'));
            if (instance) {
              instance.hide();
            }
          }, 700);
        })
        .catch(function () {
          if (status) {
            status.textContent = 'Fehler beim Speichern';
            status.classList.remove('text-success');
            status.classList.add('text-danger');
          }
        })
        .finally(function () {
          if (submitButton) {
            submitButton.disabled = false;
            submitButton.innerHTML = originalButtonText;
          }
        });
    });
  });

  function bindPluginPanelHost(host) {
    var panelsUrl = host.getAttribute('data-plugin-panels-url');
    var commandUrl = host.getAttribute('data-plugin-command-url');
    var loading = host.querySelector('[data-plugin-panel-loading]');
    var errorBox = host.querySelector('[data-plugin-panel-error]');
    var successBox = host.querySelector('[data-plugin-panel-success]');
    var list = host.querySelector('[data-plugin-panel-list]');
    var tokenInput = document.querySelector('input[name="__RequestVerificationToken"]');
    var token = tokenInput ? tokenInput.value : '';
    var modalElement = document.getElementById('admin-plugin-command-modal');
    var modalInstance = modalElement && typeof bootstrap !== 'undefined'
      ? bootstrap.Modal.getOrCreateInstance(modalElement)
      : null;
    var commandForm = modalElement ? modalElement.querySelector('[data-plugin-command-form]') : null;
    var commandTitle = modalElement ? modalElement.querySelector('[data-plugin-command-title]') : null;
    var commandMeta = modalElement ? modalElement.querySelector('[data-plugin-command-meta]') : null;
    var commandSummary = modalElement ? modalElement.querySelector('[data-plugin-command-summary]') : null;
    var commandFields = modalElement ? modalElement.querySelector('[data-plugin-command-fields]') : null;
    var submitButton = modalElement ? modalElement.querySelector('[data-plugin-command-submit]') : null;
    var currentCommand = null;
    var hasLoadedPanels = false;

    function showLoading(isVisible) {
      if (loading) {
        loading.classList.toggle('d-none', !isVisible);
      }
    }

    function showError(message) {
      if (!errorBox) {
        return;
      }

      errorBox.textContent = message || 'Plugin-Panels konnten nicht geladen werden.';
      errorBox.classList.remove('d-none');
    }

    function clearError() {
      if (!errorBox) {
        return;
      }

      errorBox.textContent = '';
      errorBox.classList.add('d-none');
    }

    function showSuccess(message) {
      if (!successBox) {
        return;
      }

      successBox.textContent = message || 'Plugin-Befehl wurde ausgefuehrt.';
      successBox.classList.remove('d-none');
    }

    function clearSuccess() {
      if (!successBox) {
        return;
      }

      successBox.textContent = '';
      successBox.classList.add('d-none');
    }

    function clearCommandErrors() {
      if (commandSummary) {
        commandSummary.textContent = '';
        commandSummary.classList.add('d-none');
      }

      if (!commandFields) {
        return;
      }

      commandFields.querySelectorAll('[data-plugin-command-feedback]').forEach(function (feedback) {
        feedback.textContent = '';
      });

      commandFields.querySelectorAll('.is-invalid').forEach(function (control) {
        control.classList.remove('is-invalid');
      });
    }

    function applyFieldErrors(fieldErrors) {
      if (!commandFields) {
        return;
      }

      (fieldErrors || []).forEach(function (error) {
        var fieldKey = String(error.fieldKey || '').toLowerCase();
        var control = commandFields.querySelector('[data-plugin-command-field="' + selectorValue(fieldKey) + '"]');
        var feedback = commandFields.querySelector('[data-plugin-command-feedback="' + selectorValue(fieldKey) + '"]');

        if (control) {
          control.classList.add('is-invalid');
        }

        if (feedback) {
          feedback.textContent = error.message || '';
        }
      });
    }

    function renderField(field) {
      var wrapperClass = field.inputType === 'MultilineText' ? 'col-12' : 'col-12 col-md-6';
      var fieldId = 'plugin-command-' + field.key;
      var constraints = field.constraints || {};
      var requiredAttr = field.required ? ' required' : '';
      var placeholderAttr = hasValue(field.placeholder) ? ' placeholder="' + escapeHtml(field.placeholder) + '"' : '';
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
        return '<div class="' + wrapperClass + '">' +
          '<div class="form-check mt-2">' +
          '<input class="form-check-input" type="checkbox" id="' + fieldId + '" data-plugin-command-field="' + escapeHtml(field.key) + '"' + requiredAttr + ' />' +
          '<label class="form-check-label" for="' + fieldId + '">' + label + '</label>' +
          '</div>' + helpText +
          '<div class="invalid-feedback d-block" data-plugin-command-feedback="' + escapeHtml(field.key) + '"></div>' +
          '</div>';
      }

      if (field.inputType === 'Number') {
        return '<div class="' + wrapperClass + '">' +
          '<label class="form-label" for="' + fieldId + '">' + label + '</label>' +
          '<input class="form-control" type="number" step="any" id="' + fieldId + '" data-plugin-command-field="' + escapeHtml(field.key) + '"' + requiredAttr + placeholderAttr + minAttr + maxAttr + ' />' +
          helpText + '<div class="invalid-feedback" data-plugin-command-feedback="' + escapeHtml(field.key) + '"></div>' +
          '</div>';
      }

      if (field.inputType === 'Date') {
        return '<div class="' + wrapperClass + '">' +
          '<label class="form-label" for="' + fieldId + '">' + label + '</label>' +
          '<input class="form-control" type="date" id="' + fieldId + '" data-plugin-command-field="' + escapeHtml(field.key) + '"' + requiredAttr + dateMinAttr + dateMaxAttr + ' />' +
          helpText + '<div class="invalid-feedback" data-plugin-command-feedback="' + escapeHtml(field.key) + '"></div>' +
          '</div>';
      }

      if (field.inputType === 'Select') {
        var options = Array.isArray(field.options)
          ? field.options.map(function (option) {
            return '<option value="' + escapeHtml(option.value) + '">' + escapeHtml(option.label) + '</option>';
          }).join('')
          : '';

        return '<div class="' + wrapperClass + '">' +
          '<label class="form-label" for="' + fieldId + '">' + label + '</label>' +
          '<select class="form-select" id="' + fieldId + '" data-plugin-command-field="' + escapeHtml(field.key) + '"' + requiredAttr + '>' +
          '<option value="">Bitte waehlen</option>' + options + '</select>' +
          helpText + '<div class="invalid-feedback" data-plugin-command-feedback="' + escapeHtml(field.key) + '"></div>' +
          '</div>';
      }

      return '<div class="' + wrapperClass + '">' +
        '<label class="form-label" for="' + fieldId + '">' + label + '</label>' +
        '<input class="form-control" type="text" id="' + fieldId + '" data-plugin-command-field="' + escapeHtml(field.key) + '"' + requiredAttr + placeholderAttr + minLengthAttr + maxLengthAttr + regexAttr + ' />' +
        helpText + '<div class="invalid-feedback" data-plugin-command-feedback="' + escapeHtml(field.key) + '"></div>' +
        '</div>';
    }

    function setFieldValues(values) {
      if (!commandFields) {
        return;
      }

      Object.keys(values || {}).forEach(function (key) {
        var control = commandFields.querySelector('[data-plugin-command-field="' + selectorValue(key.toLowerCase()) + '"]');
        if (!control) {
          return;
        }

        if (control.type === 'checkbox') {
          control.checked = String(values[key] || '').toLowerCase() === 'true';
          return;
        }

        control.value = values[key] || '';
      });
    }

    function collectArguments(schema) {
      var args = {};
      (schema || []).forEach(function (field) {
        if (!commandFields) {
          return;
        }

        var control = commandFields.querySelector('[data-plugin-command-field="' + selectorValue(field.key.toLowerCase()) + '"]');
        if (!control) {
          return;
        }

        if (field.inputType === 'Boolean') {
          args[field.key] = control.checked ? 'true' : 'false';
          return;
        }

        args[field.key] = control.value || '';
      });

      return args;
    }

    function itemSupportsCommand(item, command) {
      var schema = command.argumentSchema || [];
      if (schema.length === 0) {
        return true;
      }

      var values = item.values || {};
      return schema.every(function (field) {
        return !field.required || Object.prototype.hasOwnProperty.call(values, field.key);
      });
    }

    function renderCommandButton(moduleId, panel, command, item, extraLabel) {
      var label = extraLabel || command.label;
      var classes = 'btn btn-sm btn-' + escapeHtml(command.style || 'outline-secondary');
      return '<button type="button" class="' + classes + '"' +
        ' data-plugin-command-trigger' +
        ' data-module-id="' + escapeHtml(moduleId) + '"' +
        ' data-panel-key="' + escapeHtml(panel.key) + '"' +
        ' data-command-key="' + escapeHtml(command.key) + '"' +
        ' data-command-label="' + escapeHtml(label) + '"' +
        ' data-command-style="' + escapeHtml(command.style || 'outline-secondary') + '"' +
        ' data-command-confirm="' + escapeHtml(command.confirmMessage || '') + '"' +
        ' data-command-schema="' + escapeHtml(JSON.stringify(command.argumentSchema || [])) + '"' +
        ' data-command-values="' + escapeHtml(JSON.stringify(item ? (item.values || {}) : {})) + '">' +
        escapeHtml(label) + '</button>';
    }

    function renderPanels(modules) {
      if (!list) {
        return;
      }

      if (!modules || modules.length === 0) {
        list.innerHTML = '<div class="card border-light"><div class="card-body text-muted">Keine Plugin-Panels verfuegbar.</div></div>';
        return;
      }

      list.innerHTML = modules.map(function (module) {
        var panels = (module.panels || []).map(function (panel) {
          var commands = panel.commands || [];
          var panelCommandBar = commands.map(function (command) {
            return renderCommandButton(module.moduleId, panel, command, null, command.label);
          }).join('');

          var items = panel.items || [];
          var itemsMarkup = items.length === 0
            ? '<div class="text-muted small">Noch keine Eintraege vorhanden.</div>'
            : '<div class="table-responsive"><table class="table table-sm align-middle mb-0"><thead><tr><th>Eintrag</th><th>Status</th><th>Details</th><th class="text-end">Aktionen</th></tr></thead><tbody>' +
              items.map(function (item) {
                var itemCommands = commands.filter(function (command) { return itemSupportsCommand(item, command); }).map(function (command) {
                  return renderCommandButton(module.moduleId, panel, command, item, command.label);
                }).join(' ');
                var stateClass = item.state === 'deleted' ? 'bg-secondary' : 'bg-success';
                return '<tr>' +
                  '<td><div class="fw-semibold">' + escapeHtml(item.title) + '</div><div class="text-muted small">' + escapeHtml(item.key) + '</div></td>' +
                  '<td><span class="badge ' + stateClass + '">' + escapeHtml(item.state === 'deleted' ? 'Deaktiviert' : 'Aktiv') + '</span></td>' +
                  '<td class="small text-muted">' + escapeHtml(item.description || '') + '</td>' +
                  '<td class="text-end"><div class="d-inline-flex flex-wrap gap-2 justify-content-end">' + itemCommands + '</div></td>' +
                  '</tr>';
              }).join('') +
              '</tbody></table></div>';

          return '<div class="card border-info-subtle">' +
            '<div class="card-header bg-light d-flex flex-wrap justify-content-between align-items-start gap-2">' +
            '<div><h3 class="h6 mb-1">' + escapeHtml(panel.title) + '</h3><div class="text-muted small">' + escapeHtml(panel.description || '') + '</div></div>' +
            '<div class="d-flex flex-wrap gap-2">' + panelCommandBar + '</div>' +
            '</div>' +
            '<div class="card-body">' + itemsMarkup + '</div>' +
            '</div>';
        }).join('');

        return '<section class="d-grid gap-3">' +
          '<div><h2 class="h5 mb-1">' + escapeHtml(module.displayName) + '</h2><div class="text-muted small">Modul: ' + escapeHtml(module.moduleId) + '</div></div>' +
          panels +
          '</section>';
      }).join('');

      list.querySelectorAll('[data-plugin-command-trigger]').forEach(function (button) {
        button.addEventListener('click', function () {
          clearSuccess();
          handleCommandTrigger(button);
        });
      });
    }

    function handleCommandTrigger(button) {
      var schema = JSON.parse(button.getAttribute('data-command-schema') || '[]');
      var values = JSON.parse(button.getAttribute('data-command-values') || '{}');
      var confirmMessage = button.getAttribute('data-command-confirm') || '';

      currentCommand = {
        moduleId: button.getAttribute('data-module-id') || '',
        panelKey: button.getAttribute('data-panel-key') || '',
        commandKey: button.getAttribute('data-command-key') || '',
        label: button.getAttribute('data-command-label') || 'Plugin-Befehl',
        style: button.getAttribute('data-command-style') || 'outline-secondary',
        schema: schema,
        values: values,
        confirmMessage: confirmMessage
      };

      var canRunDirectly = schema.length === 0 || (schema.length === 1 && Object.prototype.hasOwnProperty.call(values, schema[0].key));
      if (canRunDirectly) {
        if (confirmMessage && !window.confirm(confirmMessage)) {
          return;
        }

        var directArgs = {};
        schema.forEach(function (field) {
          directArgs[field.key] = values[field.key] || '';
        });
        executeCommand(directArgs);
        return;
      }

      openCommandModal();
    }

    function openCommandModal() {
      if (!commandFields || !commandTitle || !commandMeta || !submitButton || !currentCommand) {
        return;
      }

      clearCommandErrors();
      commandFields.innerHTML = currentCommand.schema.map(renderField).join('');
      setFieldValues(currentCommand.values || {});
      commandTitle.textContent = currentCommand.label;
      commandMeta.textContent = currentCommand.confirmMessage || '';
      submitButton.textContent = currentCommand.label;
      submitButton.className = 'btn btn-' + currentCommand.style;
      modalInstance && modalInstance.show();
    }

    async function executeCommand(argumentsPayload) {
      if (!currentCommand || !commandUrl) {
        return;
      }

      clearCommandErrors();
      clearError();
      clearSuccess();

      if (submitButton) {
        submitButton.disabled = true;
      }

      try {
        var response = await fetch(commandUrl, {
          method: 'POST',
          headers: {
            'Content-Type': 'application/json',
            'RequestVerificationToken': token
          },
          body: JSON.stringify({
            moduleId: currentCommand.moduleId,
            panelKey: currentCommand.panelKey,
            commandKey: currentCommand.commandKey,
            arguments: argumentsPayload
          })
        });
        var payload = await response.json();

        if (!response.ok || !payload.success) {
          if (payload.fieldErrors && payload.fieldErrors.length > 0) {
            applyFieldErrors(payload.fieldErrors);
          }

          if (commandSummary) {
            commandSummary.textContent = payload.message || 'Plugin-Befehl fehlgeschlagen.';
            commandSummary.classList.remove('d-none');
          }
          return;
        }

        modalInstance && modalInstance.hide();
        showSuccess(payload.message || 'Plugin-Befehl wurde ausgefuehrt.');
        await loadPanels(true);
      } catch (error) {
        showError('Plugin-Befehl konnte nicht ausgefuehrt werden.');
      } finally {
        if (submitButton) {
          submitButton.disabled = false;
        }
      }
    }

    async function loadPanels(forceReload) {
      if (!panelsUrl || !list) {
        return;
      }

      if (hasLoadedPanels && !forceReload) {
        return;
      }

      showLoading(true);
      clearError();

      try {
        var response = await fetch(panelsUrl, {
          headers: {
            'Accept': 'application/json'
          }
        });
        if (!response.ok) {
          throw new Error('load failed');
        }

        var payload = await response.json();
        renderPanels(payload || []);
        hasLoadedPanels = true;
      } catch (error) {
        showError('Plugin-Panels konnten nicht geladen werden.');
      } finally {
        showLoading(false);
      }
    }

    if (commandForm) {
      commandForm.addEventListener('submit', function (event) {
        event.preventDefault();

        if (!currentCommand) {
          return;
        }

        if (commandForm.checkValidity && !commandForm.checkValidity()) {
          commandForm.reportValidity();
          return;
        }

        executeCommand(collectArguments(currentCommand.schema));
      });
    }

    var pluginTabButton = document.getElementById('plugins-tab');
    if (pluginTabButton) {
      pluginTabButton.addEventListener('shown.bs.tab', function () {
        loadPanels(false);
      });
    }

    if (document.getElementById('plugins') && document.getElementById('plugins').classList.contains('show')) {
      loadPanels(false);
    }
  }
})();
