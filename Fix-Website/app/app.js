(function() {
    var app = angular.module('powershellSite', []);

    app.directive("contentTable", function(){
        return {
            restrict: 'E',
            templateUrl: "Templates/content-table.html",
            controller: function($scope) {
                this.expanded = false;
                this.cmdlet = false;
                this.clickProduct = function () {
                    if (this.cmdlet) {
                        $scope.setFrame(2);
                        this.cmdlet = false;
                    }
                    else {
                        this.expanded = !this.expanded;
                        if (this.expanded) {
                            $scope.setFrame(2);
                        } else {
                            $scope.setFrame(1);
                        }
                    }
                };
                this.clickCmdlet = function () {
                    this.cmdlet = true;
                    $scope.setFrame(3);
                };
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

    app.controller('ReferenceController', ['$scope', function ($scope) {
        $scope.frame = 1;
        $scope.checkFrame = function (check) {
            return this.frame === check;
        };
        $scope.setFrame = function (newFrame) {
            this.frame = newFrame;
        };

    }]);
})();