// Group-aware search + type filtering for the Members overview (Slice 4).
//
// The Members Index renders a flattened hierarchy: every container parent (depth 0) is
// immediately followed by its indented sub-members (depth 1). Each rendered row/card carries
// data-group-id (the container/parent id, identical for every row in a group),
// data-group-type (the container/parent type key, identical for every row in a group),
// data-member-type (the row's own type key) and data-depth.
//
// Filtering operates per GROUP, never per row, so a match on a parent OR any of its
// sub-members reveals the whole group together (indentation preserved), and filtering by a
// container type keeps that container's sub-members visible even when their own type differs.
(function (global) {
    'use strict';

    // Pure, DOM-free decision. Given row descriptors ({ groupId, groupType, memberType, text }),
    // the selected type `filter` and a search `term`, returns an object mapping every groupId
    // (as a string) to whether the whole group should be visible.
    function computeGroupVisibility(rows, filter, term) {
        var groups = {};
        var i, r, key;

        for (i = 0; i < rows.length; i++) {
            r = rows[i];
            key = String(r.groupId);
            if (!groups[key]) {
                groups[key] = { groupType: r.groupType, memberTypes: [], texts: [] };
            }
            groups[key].memberTypes.push(r.memberType);
            groups[key].texts.push((r.text || '').toLowerCase());
        }

        var normalizedTerm = (term || '').toLowerCase();
        var result = {};
        for (key in groups) {
            if (!Object.prototype.hasOwnProperty.call(groups, key)) {
                continue;
            }
            var g = groups[key];
            var typeMatch = (filter === 'all')
                || (filter === g.groupType)              // container type keeps every sub visible
                || (g.memberTypes.indexOf(filter) > -1); // any own type within the group
            var searchMatch = false;
            for (i = 0; i < g.texts.length; i++) {
                if (g.texts[i].indexOf(normalizedTerm) > -1) {
                    searchMatch = true;
                    break;
                }
            }
            result[key] = typeMatch && searchMatch;
        }
        return result;
    }

    // DOM entry point wired from the type-filter change and the search input.
    function filterMembers() {
        var typeFilterEl = document.getElementById('typeFilter');
        var searchEl = document.getElementById('searchInput');
        var filter = typeFilterEl ? typeFilterEl.value : 'all';
        var term = searchEl ? searchEl.value : '';

        var rowEls = document.querySelectorAll('.member-table tbody tr[data-group-id], .card[data-group-id]');
        var descriptors = [];
        var i, el;
        for (i = 0; i < rowEls.length; i++) {
            el = rowEls[i];
            descriptors.push({
                groupId: el.getAttribute('data-group-id'),
                groupType: el.getAttribute('data-group-type'),
                memberType: el.getAttribute('data-member-type'),
                text: el.textContent || ''
            });
        }

        var visibility = computeGroupVisibility(descriptors, filter, term);
        for (i = 0; i < rowEls.length; i++) {
            el = rowEls[i];
            var gid = String(el.getAttribute('data-group-id'));
            el.style.display = visibility[gid] ? '' : 'none';
        }
    }

    global.computeGroupVisibility = computeGroupVisibility;
    global.filterMembers = filterMembers;

    if (typeof module !== 'undefined' && module.exports) {
        module.exports = { computeGroupVisibility: computeGroupVisibility, filterMembers: filterMembers };
    }
})(typeof window !== 'undefined' ? window : this);
