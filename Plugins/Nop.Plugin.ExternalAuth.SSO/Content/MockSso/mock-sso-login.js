/*
 * Mock IdP login page (DEBUG builds only) — Views/MockSso/Login.cshtml.
 *
 * Shows the Groups input only for the "With groups" scenario, and swaps the email hint to the
 * text on the selected scenario radio (data-hint). The server ignores the groups value for any
 * other scenario; hiding it keeps the page focused on what each scenario exercises.
 */
(function () {
    var groupsGroup = document.getElementById('groupsGroup');
    var emailHint = document.getElementById('emailHint');
    var radios = document.querySelectorAll('input[name="Scenario"]');

    if (!groupsGroup || !emailHint || !radios.length)
        return;

    function applyScenario() {
        var checked = document.querySelector('input[name="Scenario"]:checked');
        if (!checked)
            return;

        groupsGroup.classList.toggle('hidden', checked.value !== 'success-with-groups');
        emailHint.textContent = checked.getAttribute('data-hint') || '';
    }

    radios.forEach(function (r) { r.addEventListener('change', applyScenario); });
    applyScenario();
})();
