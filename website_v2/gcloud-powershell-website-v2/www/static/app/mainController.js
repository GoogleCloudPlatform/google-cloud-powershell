var app = angular.module('powershellSite');

app.controller('MainController',
    function($scope, $route, $routeParams, $location, $http) {
    $scope.$route = $route;
    $scope.$location = $location;
    $scope.$routeParams = $routeParams;
    $http.get('static/_data/cmdletsFull.json')
       .then(function(res) {
           $scope.products = res.data;
       });
});
