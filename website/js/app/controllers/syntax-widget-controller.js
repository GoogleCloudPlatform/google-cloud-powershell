var app = angular.module('powershellSite');

/**
 * Syntax Widget for rendering a color-coded view of a cmdlet's syntax (parameter set).
 * The main functionality of the controller is to report/respond to click events to
 * select/unselect a specific parameter set.
 */
app.controller('SyntaxWidgetController', function($scope) {
    // Whether or not this parameter set is selected.
    this.isSelected = false;

    // When this syntax directive is clicked, notify our parent controller who will notify other
    // directives to update their UI accordingly. We don't update isSelected until we respond
    // to the parameterSetSelected event.
    this.onClick = function(parameterSetName) {
        if (!this.isSelected) {
            $scope.$parent.onParameterSetSelected(parameterSetName);
        } else {
            $scope.$parent.onParameterSetDeselected();
        }
    };
    $scope.$on('parameterSetSelected', function(event, parameterSetName) {
        this.isSelected = (parameterSetName == $scope.syntax.parameterSet);
    }.bind(this));

    // Returns whether or not to show a bracket ('[' or ']') for a parameter in the given location.
    // PowerShell formats the help documentation for parameters up to four different ways.
    // See: https://technet.microsoft.com/en-us/library/hh847867.aspx
    //
    // <command-name> -<Required Parameter Name> <Required Parameter Value>
    //                 [-<Optional Parameter Name> <Optional Parameter Value>]
    //                 [-<Optional Switch Parameters>]
    //                 [-<Optional Parameter Name>] <Required Parameter Value>
    //
    // Location is one of "beforeName", "afterName", or "afterType".
    this.showBracket = function(parameter, location) {
        // Show a bracket in front of optional or positional parameters.
        if (location == 'beforeName') {
            return ((parameter.required == 'false') || (parameter.position != 'named'));
        }
        // Show a bracket after the parameter name if its value is still required. i.e. the
        // parameter is positional.
        if (location == 'afterName') {
            return (parameter.position != 'named');
        }
        // Show the parameter after the type if we started a bracket, but didn't already
        // finish it.
        if (location == 'afterType') {
            return (
                this.showBracket(parameter, 'beforeName') &&
                !this.showBracket(parameter, 'afterName'));
        }
        return false;
    };
});
