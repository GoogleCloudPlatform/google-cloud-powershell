var app = angular.module('powershellSite');

app.controller('MainController',
    function($scope, $route, $routeParams, $location, $http) {
    $scope.$route = $route;
    $scope.$location = $location;
    $scope.$routeParams = $routeParams;
    try {
    $http.get('static/_data/cmdletsFull.json')
       .then(function(res) {
           /** We store the json info on the scope so everything has access **/
           $scope.productInfo = res.data;
       });
    }
    catch (err) {
        $scope.products = null;
        console.error(err);
    }
});
