var app = angular.module('powershellSite');

/**
 * Parameter Widget for rendering the parameters of a cmdlet. If the user
 * selects a specific cmdlet, we do some book keeping so that relevant
 * parameters are highlighted or hidden.
 */
app.controller('ParametersWidgetController', function($scope) {
    // The currently selected parameter set (determined by a SyntaxWidget). The ParameterWidget
    // updates its view to show/hide/obscure parameters that are more or less relevant.
    this.selectedParameterSet = null;

    $scope.$on('parameterSetSelected', function(event, parameterSetName) {
        this.selectedParameterSet = parameterSetName;
    }.bind(this));

    // Returns whether or not the provided parameter is in the currently selected parameter set.
    this.inSelectedParamSet = function(parameter) {
        if (!parameter || !parameter.parameterSet) {
            return false;
        }
        return (parameter.parameterSet.indexOf(this.selectedParameterSet) != -1);
    };

    // Ordering function passed to ng-repeat. The function is called for each parameter and returns
    // an object for sorting. We generally order by parameter set but favor selectedParameterSet.
    $scope.orderByParamSet = function(favoredParamSet) {
        return function(parameter) {
            if (!parameter) return '';

            // The parameters are in the order they are decalred in the cmdlet's implementation. So
            // generally most important first. We preserve this random ordering, and only provided
            // hints based on parameter set.
            if (parameter.parameterSet && parameter.parameterSet.indexOf(favoredParamSet) != -1) {
                return 'a-' + parameter.name;
            }
            return 'b-' + parameter.name;
        };
    };
});
