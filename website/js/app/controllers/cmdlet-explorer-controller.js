var app = angular.module('powershellSite');

/**
 * CmdletExplorerController is a widget to display a tree-view like navigator to browse GCP
 * products, resources, and individual cmdlets. The logic here simply controls which
 * product or resource has been selected.
 */
app.controller('CmdletExplorerController', function($scope, $routeParams) {
  // The current product, resource, or cmdlet. As determined by the URL route parameters.
  this.currentProduct = '';
  this.currentResource = '';
  this.currentCmdlet = '';

  // $routeParams is populated asynchronously. So we need to delay reading the route params.  
  $scope.$on('$routeChangeSuccess', function() {
    this.selectedProduct  = this.currentProduct = $routeParams['product'] || '';
    this.selectedResource = this.currentResource = $routeParams['resource'] || '';
    this.currentCmdlet = $routeParams['cmdlet'] || '';
  }.bind(this));

  // The currently selected product or resource. This is dependent upon the user's choice. e.g.
  // selecting the same product will "unselect" it, but the URL is the same.
  this.selectedProduct = '';
  this.selectedResource = '';

  // Select a product/resource. Selecting it twice will unselect it.
  this.selectProduct = function(product) {
    if (this.selectedProduct == product) {
      product = '';
    }
    this.selectedProduct = product;
  };

  this.selectResource = function(resource) {
    if (this.selectedResource == resource) {
      resource = '';
    }
    this.selectedResource = resource;
  };

  // Return whether or not the given product/resource is selected.
  this.isProductSelected = function(product) {
    return (this.selectedProduct == product);
  };

  this.isResourceSelected = function(resource) {
    return (this.selectedResource == resource);
  };

  // Returns whether or not we should highlight the product, resource, or cmdlet. We only highlight
  // a row if we don't know what is "below" it. e.g. if a cmdlet is selected, we don't highlight
  // the parent resource and product. 
  this.isHighlighted = function(kind, name) {
    if (kind == 'product') {
      return (name == this.currentProduct && this.currentResource == '');
    }
    if (kind == 'resource') {
      return (name == this.currentResource && this.currentCmdlet == '');
    }
    if (kind == 'cmdlet') {
      return (name == this.currentCmdlet);
    }
    return false;
  }
});
