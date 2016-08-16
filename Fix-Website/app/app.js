(function() {
    var app = angular.module('powershellSite', []);

    app.directive("contentTable", function(){
        return {
            restrict: 'E',
            templateUrl: "Templates/content-table.html",
            controller: function($scope) {
                this.expanded = false;
                this.activeProduct = "";
                this.clickProduct = function (name) {
                    if (this.activeProduct === name) {
                        $scope.setFrame(1,"basic");
                        this.activeProduct = "";
                    }
                    else {
                        $scope.setFrame(2,name);
                        this.activeProduct = name;
                    }
                };
                this.clickCmdlet = function (name) {
                    $scope.setFrame(3,name);
                };
                this.isExpanded = function (name) {
                    return (this.activeProduct === name);
                }
            },
            controllerAs: 'table'
        };
    });

    app.directive("infoZone", function () {
        return {
            restrict: 'E',
            templateUrl: "Templates/info.html"
        };
    });

    /**
     * Controller for setting up the scope.
     */
    app.controller('ReferenceController', ['$scope','$http', function ($scope, $http) {
        $scope.frame = 1;
        $scope.active = "basic";
        $scope.activeProduct = "";
        $scope.checkFrame = function (check) {
            return $scope.frame === check;
        };
        $scope.setFrame = function (newFrame, newActive) {
            if (newFrame === 3 & $scope.frame != 3) {
                $scope.activeProduct = $scope.active;
            }
            $scope.frame = newFrame;
            $scope.active = newActive;
        };
        $http.get('cmdletsfull.json')
            .then(function (res) {
                $scope.products = res.data;

            });
    }]);
    /*Angular doesn't sort objects, only arrays, so I had to make my own filter.*/
    app.filter('orderByCmdletProp', function () {
        return function (cmdlets, cmdletField, reverse) {
            var filtered = [];
            angular.forEach(cmdlets, function (cmdlet) {
                filtered.push(cmdlet);
            });
            filtered.sort(function (a, b) {
                return (a[cmdletField] > b[cmdletField] ? 1 : -1);
            });
            if (reverse) filtered.reverse();
            return filtered;
        };
    });
})();
