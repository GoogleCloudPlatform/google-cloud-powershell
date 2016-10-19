// When the window is small, #header-icon becomes a toggleable menu icon which will show/hide
// #left-nav. (Which will then float on top of #content-container.) This file implements the
// behavior of the menu icon.
//
// TODO(chrsmith): If this file gets any more complicated, consider moving it into a proper
// Angular controller. (e.g. ContentController or a new NavigationController.) Registering
// a watch on document.body.clientWidth should be sufficient to obviating a lot of the code
// here.
(function() {
    var headerIcon = document.getElementById('header-icon');
    var leftNav = document.getElementById('left-nav');
    var contentContainer = document.getElementById('content-container');
    if (!(headerIcon && leftNav && contentContainer)) {
        // ERROR: We didn't find the required elements on the page.
        return;
    }

    function isScreenSmall() {
        // Should match body.css, left-nav.css.
        var windowWidth = document.body.clientWidth;
        return (windowWidth < 1160);
    }

    function isNavVisible() {
        return (leftNav.style.display != 'none');
    }
    function setNavDisplay(visible) {
        leftNav.style.display = visible ? 'block' : 'none';
    }

    // Register the toggle show/hide behavior.
    headerIcon.addEventListener('click', function() {
        if (isScreenSmall()) {
        var navVisibility = isNavVisible();
        setNavDisplay(!navVisibility);
        }      
    });

    // Handle window resize so the nav is always visible when possible.
    window.addEventListener('resize', function() {
        if (!isScreenSmall()) {
        setNavDisplay(true);
        }
    });

    // Start in the collapsed state if the window starts small (e.g. on mobile).
    window.addEventListener('load', function() {
        if (isScreenSmall()) {
        setNavDisplay(false);
        }
    });

    // Enable clicking outside of the nav (e.g. content) to close it.
    contentContainer.addEventListener('click', function() {
        if (isScreenSmall()) {
        setNavDisplay(false);
        }
    });

    // End of the anonymous JavaScript module.
})();