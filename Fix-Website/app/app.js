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
                        $scope.setFrame(1);
                        this.activeProduct = "";
                    }
                    else {
                        $scope.setFrame(2);
                        this.activeProduct = name;
                    }
                };
                this.clickCmdlet = function (name) {
                    $scope.setFrame(3);
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
            templateUrl: "Templates/info.html",
            controller: function () {
                this.form = false;
                this.clickProduct = function () {

                    this.form = !this.form;
                };
            },
            controllerAs: 'info'
        };
    });

    app.controller('ReferenceController', ['$scope','$http', function ($scope, $http) {
        $scope.frame = 1;
        $scope.checkFrame = function (check) {
            return this.frame === check;
        };
        $scope.setFrame = function (newFrame) {
            this.frame = newFrame;
        };
        $http.get('cmdlets.json')
            .then(function (res) {
                $scope.cmdlets = res.data;
            });
    }]);
})();
