// Behavior harness for the group-aware Members filter (Slice 4).
// Executed by MemberIndexGroupFilterTests via the installed Node runtime. It requires the
// production module (path passed as argv[2]) and exercises the three DoD scenarios against
// the pure computeGroupVisibility core. Exits non-zero (with a FAIL message) on any mismatch.
'use strict';

var modulePath = process.argv[2];
var mod = require(modulePath);
var computeGroupVisibility = mod.computeGroupVisibility;

function assert(condition, message) {
    if (!condition) {
        console.error('FAIL: ' + message);
        process.exit(1);
    }
}

// A "Firma" container group (id 1): the Firma parent plus one own-type "Standard" sub-member,
// and a separate unrelated top-level "Standard" member (its own group, id 5).
function rows() {
    return [
        { groupId: '1', groupType: 'Firma',    memberType: 'Firma',    text: 'Anton Alpha Firma' },
        { groupId: '1', groupType: 'Firma',    memberType: 'Standard', text: 'Bea Beta Mitarbeiter' },
        { groupId: '5', groupType: 'Standard', memberType: 'Standard', text: 'Cara Gamma' }
    ];
}

// Scenario 1: a sub-member matches the search term -> its WHOLE group is shown; groups that
// do not match are hidden.
var vis = computeGroupVisibility(rows(), 'all', 'bea');
assert(vis['1'] === true, 'child match on search reveals the whole group (group 1 visible)');
assert(vis['5'] === false, 'non-matching group stays hidden (group 5)');

// Scenario 2: filtering by a container type keeps that container's differently-typed
// sub-members visible (the whole Firma group), while unrelated groups are hidden.
vis = computeGroupVisibility(rows(), 'Firma', '');
assert(vis['1'] === true, 'filter by container type Firma keeps its Standard sub visible (group 1)');
assert(vis['5'] === false, 'filter by Firma hides the unrelated Standard group (group 5)');

// Scenario 3: nothing matches the search term -> the whole group is hidden.
vis = computeGroupVisibility(rows(), 'all', 'zzz-does-not-exist');
assert(vis['1'] === false, 'no match hides the whole group (group 1)');
assert(vis['5'] === false, 'no match hides the whole group (group 5)');

// Bonus: an own-type match inside a group also reveals it (type filter on a member type).
vis = computeGroupVisibility(rows(), 'Standard', '');
assert(vis['1'] === true, 'own-type Standard member in group 1 makes the group visible');
assert(vis['5'] === true, 'own-type Standard group 5 visible');

console.log('ALL PASS');
