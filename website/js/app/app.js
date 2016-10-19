(function() {
    // Prefix where HTML partials/templates will be found.
    var prefix = '/google-cloud-powershell/js/app/controllers/';
    
    var app = angular.module('powershellSite', ['ngRoute', 'ngSanitize']);
    app.config(function($routeProvider, $locationProvider) {
        $routeProvider
            .when('/',                           { controller: 'ContentController', templateUrl: prefix + 'content-homepage.ng' })
            .when('/:product',                   { controller: 'ContentController', templateUrl: prefix + 'content-product.ng'  })
            .when('/:product/:resource',         { controller: 'ContentController', templateUrl: prefix + 'content-resource.ng' })
            .when('/:product/:resource/:cmdlet', { controller: 'ContentController', templateUrl: prefix + 'content-cmdlet.ng'   });
        // Disabling because we don't control redirection, e.g. we cannot serve
        // index.html for any URL under X. This means that  you'll get a 404
        // for deep links. So, alas, we need the anchor.
        $locationProvider.html5Mode(false);
    });

    // Directive for the left-nav, the table of contents.
    app.directive('cmdletExplorer', function() {
        return {
            restrict: 'E',
            templateUrl: prefix + 'cmdlet-explorer.ng',
            controller: 'CmdletExplorerController',
            controllerAs: 'cmdletExplorerCtrl',
            scope: {
                documentationObj: '='
            }
        };
    });

    // Directive for rendering a PowerShell cmdlet's syntax and parameters.
    app.directive('syntaxWidget', function() {
        return {
            restrict: 'E',
            templateUrl: prefix + 'syntax-widget.ng',
            controller: 'SyntaxWidgetController',
            controllerAs: 'syntaxCtrl',
            scope: {
                syntax: '='
            }
        };
    });

    // Directive for rendering a PowerShell cmdlet's parameters.
    app.directive('parametersWidget', function() {
        return {
            restrict: 'E',
            templateUrl: prefix + 'parameters-widget.ng',
            controller: 'ParametersWidgetController',
            controllerAs: 'parametersCtrl',
            scope: {
                parameters: '='
            }
        };
    });

    // Directive for referencing a known cmdlet. The result has the expected CSS styling and a
    // link to the cmdlet's documentation. e.g. <cmdlet-ref name="Get-GceInstance"></cmdlet-ref>
    app.directive('cmdletRef', function() {
        return {
            restrict: 'E',
            template: '<span ng-bind-html="name | cmdletRef"></span>',
            scope: {
                name: '@'
            }
        };
    });

    // Filter for converting a cmdletname into reference HTML, e.g. "Get-GcsObject" into
    // <a href="..."><code class="cmdlet">Get-GcsObject</code></a>. Only useful if used in
    // conjunction with ng-bind-html. See the cmdletRef directive.
    function getGenerateCmdletRefFn($rootScope) {
        return function(cmdletName) {
            // Replace '-' with a non-breaking hyphen.
            var revisedCmdletName = cmdletName.replace('-', '&#8209;');
            var cmdletRefHtml = '<code class="cmdlet">' + revisedCmdletName + '</code>';
            // If the cmdlet documentation has been attached to $rootScope, see if we can
            // create a link to the actual cmdlet. (See content-controller.js.) Note: This
            // is a no-op if the element with applyHtmlStyling being applied is housed
            // within another anchor element.
            if (!$rootScope.cmdletDocumentation) {
                return cmdletRefHtml;
            }

            var docs = $rootScope.cmdletDocumentation;
            for (var prodIdx = 0; prodIdx < docs.products.length; prodIdx++) {
                var product = docs.products[prodIdx];
                for (var resIdx = 0; resIdx < product.resources.length; resIdx++) {
                    var resource = docs.products[prodIdx].resources[resIdx];
                    for (var cIdx = 0; cIdx < resource.cmdlets.length; cIdx++) {
                        var cmdlet = docs.products[prodIdx].resources[resIdx].cmdlets[cIdx];
                        if (cmdlet.name == cmdletName) {
                            return (
                                '<a href="/google-cloud-powershell/#/'
                                    + product.name
                                    + '/' + resource.name
                                    + '/' + cmdlet.name + '">'
                                    + cmdletRefHtml + '</a>');
                        }
                    }
                }
            }
            return cmdletRefHtml;
        };
    };
    app.filter('cmdletRef', getGenerateCmdletRefFn);

    // Filter to extract the simplified name from a full-qualified .NET type name. e.g.
    // "System.String" to "String".
    app.filter('stripNamespace', function() {
        var genericListPattern = /(.+Generic.List`1\[\[)([^,]+)(, .*\]\])/g
        return function(rawInput) {
            var input = rawInput || '';

            // HACK: Some cmdlets (Add-GceFirewall) accept Lists<T> for their parameters instead of
            // simple arrays. This is a design flaw in the cmdlet. But we paper over them for now,
            // until we clean up our cmdlets. For now we special case it.
            if (input.indexOf('Generic.List`1') != -1) {
                input = input.replace(genericListPattern, function(m, before, type, after) {
                    return type + '[]';
                });
            }

            // Location of the last namespace separator (.), nested type delimiter (+), or -1.
            // e.g. System.String, Namespace.ParentType+NestedType.
            var splitPoint = Math.max(
                input.lastIndexOf('.'),
                input.lastIndexOf('+'));
            return input.substring(splitPoint + 1);
        };
    });

    // Filter for rewriting raw text as HTML. To be passed to the ng-bind-html directive, which
    // also takes care of sanitization.
    // - Converts arrays of strings to separate <p> elements.
    // - Puts quoted strings in <code class="text"> elements.
    // - Puts references to cmdlets in <code class="cmdlet"> elements.
    app.filter('applyHtmlStyling', function($rootScope) {
        // Outside of the function to avoid recompiling them every time the filter is invoked.
        var quotedStringPattern = /([^"]*)"([^"]+)"([^"]*)/g;
        var cmdletReferencePattern = /[A-Z]([\w]+)-[A-Z]([\w]+)/g

        return function(rawInput) {
            if (rawInput == undefined ) return;
            var lines = rawInput;
            if (!Array.isArray(rawInput)) {
                lines = [ rawInput ];
            }

            var html = '';
            for (var lineIdx = 0; lineIdx < lines.length; lineIdx++) {
                // Each line of the description is its own <p> element.
                html += '<p>';

                var rawLine = lines[lineIdx];
                rawLine = rawLine.replace(quotedStringPattern, function(x, before, text, after) {
                    return before + '<code class="text">"' + text + '"</code>' + after;
                });
                html += rawLine;

                html += '</p>';
            }

            // Use regular expressions to find/modify cmdlet references.
            html = html.replace(cmdletReferencePattern, getGenerateCmdletRefFn($rootScope));
            return html;
        };
    });

// End of the anonymous JavaScript module. 
})();
