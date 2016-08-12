(function() {
    var app = angular.module('powershellSite', []);

   /* app.directive("contentTable", function(){
        return {
            restrict: 'E',
            templateUrl: "content-table.html",
            controller: function() {
                this.expanded = false;
                this.cmdlet = false;
                this.clickProduct = function (ref) {
                    if (this.cmdlet) {
                        ref.setFrame(2);
                        this.cmdlet = false;
                    }
                    else {
                        this.expanded = !this.expanded;
                        if (this.expanded) {
                            ref.setFrame(2);
                        } else {
                            ref.setFrame(1);
                        }
                    }
                };
                this.clickCmdlet = function (ref) {
                    this.cmdlet = true;
                    ref.setFrame(3);
                };
            },
            controllerAs: 'table'
        };
    });*/

    app.controller('TableController', ['$scope', function ($scope) {
        this.expanded = false;
        this.cmdlet = false;
        this.clickProduct = function (ref) {
            if (this.cmdlet) {
                ref.setFrame(2);
                this.cmdlet = false;
            }
            else {
                this.expanded = !this.expanded;
                if (this.expanded) {
                    ref.setFrame(2);
                } else {
                    ref.setFrame(1);
                }
            }
        };
        this.clickCmdlet = function (ref) {
            this.cmdlet = true;
            ref.setFrame(3);
        };
    }]);
    
    app.controller('InfoController', ['$scope', function ($scope) {
        this.form = false;
        this.clickProduct = function () {

            this.form = !this.form;
        };

    }]);

    app.controller('ReferenceController', ['$scope', function ($scope) {
        this.frame = 1;
        this.checkFrame = function (check) {
            return this.frame === check;
        };
        this.setFrame = function (newFrame) {
            this.frame = newFrame;
        };

    }]);
})();