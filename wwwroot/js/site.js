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
