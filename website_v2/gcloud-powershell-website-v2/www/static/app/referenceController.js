var app = angular.module('powershellSite');

/** The controller that controls url parameters. **/
app.controller('ReferenceCtrl', ['$scope', '$routeParams',
    function($scope, $routeParams) {
    this.params = $routeParams;
}]);
