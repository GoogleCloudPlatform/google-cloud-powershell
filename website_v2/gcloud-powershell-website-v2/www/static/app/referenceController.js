var app = angular.module('powershellSite');

/** The controller that exposes route parameters to the templates. **/
app.controller('ReferenceController', ['$scope', '$routeParams',
    function($scope, $routeParams) {
        this.params = $routeParams;
}]);
