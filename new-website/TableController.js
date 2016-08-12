app.controller('TableController', ['$scope', function ($scope) {
    this.form = false;
    $scope.clickProduct = function (product) {
        $scope.expand(product);
        this.form = true;
    };
    $scope.expand = function (product) {
        $scope.display = product;
    };
}]);