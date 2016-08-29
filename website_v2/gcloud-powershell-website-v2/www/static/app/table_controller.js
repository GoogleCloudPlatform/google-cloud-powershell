var app = angular.module('powershellSite');

/* The controller for the table of contents. */
app.controller('TableController', function($scope, $attrs) {
  /* Whether or not a product's information is expanded. */
  this.expanded = false;
  this.productInfo = $scope.productInfo;

  /** What the current active product is. */
  this.activeProduct = '';

  /**
   * onProductClick is used when a product is clicked.
   * 'productName' is the product clicked.
   */
  this.onProductClick = function(productName) {
    /* It either closes the current expansion and shows the home page */
    if (this.activeProduct === productName) {
      this.activeProduct = '';
    }
    /**
     * Or it sets the information to be the information screen for
     * the applicable product.
     */
    else {
      this.activeProduct = productName;
    }
  };

  /**
   * isExpanded just tells whether or not a product is expanded
   * 'productName' is the product we are checking.
   */
  this.isExpanded = function(productName) {
    return (this.activeProduct === productName);
  };
});
