// Sign-in page behaviour: password visibility toggle and submit state.
// Kept in an external file so the strict Content-Security-Policy does not need inline script.
(function () {
  'use strict';

  function initPasswordToggle() {
    var toggle = document.getElementById('password-toggle');
    if (!toggle) {
      return;
    }

    var input = document.getElementById(toggle.getAttribute('aria-controls'));
    if (!input) {
      return;
    }

    var showLabel = toggle.getAttribute('data-show-label') || 'Show password';
    var hideLabel = toggle.getAttribute('data-hide-label') || 'Hide password';

    toggle.addEventListener('click', function () {
      var visible = input.type === 'text';
      input.type = visible ? 'password' : 'text';
      toggle.setAttribute('aria-pressed', String(!visible));
      toggle.textContent = visible ? showLabel : hideLabel;
      input.focus();
    });
  }

  function initSubmitState() {
    var form = document.getElementById('login-form');
    if (!form) {
      return;
    }

    var button = form.querySelector('button[type="submit"]');
    if (!button) {
      return;
    }

    var idleText = button.textContent;
    var submitting = button.getAttribute('data-submitting-text');

    // The submit event only fires once HTML5 validation passes. Disabling the button stops
    // double submissions (double click, Enter key) from counting twice against the lockout.
    form.addEventListener('submit', function (event) {
      if (button.disabled) {
        event.preventDefault();
        return;
      }

      button.disabled = true;
      button.setAttribute('aria-disabled', 'true');
      if (submitting) {
        button.textContent = submitting;
      }
    });

    // Restore the button when the page comes back from the back-forward cache still in a submitting state.
    window.addEventListener('pageshow', function () {
      button.disabled = false;
      button.removeAttribute('aria-disabled');
      button.textContent = idleText;
    });
  }

  // Language picker submits its GET form on change; the noscript button covers browsers without JavaScript.
  function initLanguageSwitch() {
    var select = document.getElementById('lang-select');
    var form = document.getElementById('lang-form');
    if (!select || !form) {
      return;
    }

    select.addEventListener('change', function () {
      form.submit();
    });
  }

  document.addEventListener('DOMContentLoaded', function () {
    initPasswordToggle();
    initSubmitState();
    initLanguageSwitch();
  });
})();
