var app = angular.module('powershellSite');

app.controller('MainController',
    function($scope, $http) {
    try {
        $http.get('static/_data/cmdletsFull.json')
       .then(function(res) {
            /** We store the json info on the scope so everything has access **/
            $scope.productInfo = res.data;
       });
    }
    catch (err) {
        $scope.productInfo = null;
        console.error(err);
    }
});
