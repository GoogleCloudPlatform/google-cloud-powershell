var app = angular.module('powershellSite');

/**
 * Controller for the main content page. All views (product, resource, cmdlet) use this controller.
 * We load all cmdlet documentation (a giant JSON file) and populate fields based what is currently
 * being displayed.
 */
app.controller('ContentController', function($scope, $rootScope, $http, $routeParams) {
  // The header to be displayed on the content page. This isn't referenced in
  // .ng files because of a quirk of the current design. (How the header's
  // background spans the entire page, not just the "content" section.)
  var defaultContentHeader = "Google Cloud Tools for PowerShell";
  this.contentHeader = null;
  // Error message to display in case of an error loading data.
  this.errorMessage = null;

  // Current product, resource, and cmdlet. Some may be null. e.g.
  // "Google Cloud Storage" > "GcsObject" > "Get-GcsObject". However, we always set the values,
  // because in the cases where they are not relevant, the .ng file won't require them.
  this.currentProduct = null;
  this.currentResource = null;
  this.currentCmdlet = null;

  // Multi-level dictionary containing ALL cmdlet documentation. First keyed by product, then
  // keyed by resources, finally keyed by cmdlet names, mapping to cmdlet documentation objects.
  // Loaded on application startup, but then cached via the $http service.
  // TODO(chrsmith): As this file gets larger and larger, break it into multiple pieces. Perhaps
  // loading cmdlet-specific JSON objects.
  this.cmdletDocumentation = null;
  // We also attach the documentation object to $rootScope.cmdletDocumentation once loaded. This
  // way we don't need to redo all the parsing/processing of the JSON.

  // Information about the product, resource, or cmdlet. To be set in _loadContent after both
  // cmdletDocumentation and $routeParams have been set.
  this.productInfo = null;
  this.resourceInfo = null;
  this.cmdletInfo = null;

  // $routeParams is populated asynchronously. So we need to delay reading the route params.  
  $scope.$on('$routeChangeSuccess', function() {
    this.currentProduct = $routeParams['product'] || '';
    this.currentResource = $routeParams['resource'] || '';
    this.currentCmdlet = $routeParams['cmdlet'] || '';
    this._loadContent();
  }.bind(this));

  // onParameterSetSelected is called (via a child SyntaxWidgetController) whenever the user
  // selects a parameter set. Then this controller (parent of various child directives) will
  // broadcast a message "parameterSetSelected" with the arg of the selected parameter set.
  // Child controllers can subscribe to the message as needed.
  $scope.onParameterSetSelected = function(parameterSetName) {
    $scope.$broadcast("parameterSetSelected", parameterSetName);
  };
  // onParameterSetDeselected works the same, but broadcases out a 'null' parameter set.
  $scope.onParameterSetDeselected = function() {
    $scope.$broadcast("parameterSetSelected", null);
  };

  // There are some quirks with the way our script generates JSON data. Specifically, arrays
  // with a single element are serialized as the object and not an array with one object.
  // We manually fix these objects up so that we avoid needing to special case the controllers.
  //
  // The function takes a string similar to an object query, to walk down an object hierarchy.
  // * meaning all fields.
  this._makeArray = function(path, sourceObj) {
    // Convert the field into an array if needed.
    function makeFieldArray(fieldName, obj) {
      var fieldValue = obj[fieldName];
      if (fieldValue == null) {
        obj[fieldName] = [];
      } else if (!Array.isArray(fieldValue)) {
        obj[fieldName] = [fieldValue];
      }
    }

    // Walk down the object properties until you get to the specific field you want to upgrade.
    // @type {Array.<string>} fields The array of fields to check.
    // @type {Object} obj The object to update.
    function walkTypeProperties(fields, obj) {
      if (!obj) {
        return;
      }
      // If the object is an array, walk down all ements of it.
      if (Array.isArray(obj)) {
        for (var i = 0; i < obj.length; i++) {
          walkTypeProperties(fields, obj[i]);
        }
        return;
      }

      var nextFieldName = fields[0];
      // Done walking the type. Finally make the field an array.
      if (fields.length == 1) {
        // BUG: If the fields list ends in a '*', we don't do the right thing.
        makeFieldArray(nextFieldName, obj)
        return;
      }
      // Recurse walking down the next field. The field name '*' means to recurse checking
      // all object properties.
      if (nextFieldName != '*') {
        walkTypeProperties(fields.slice(1), obj[nextFieldName]);
      } else {
        for (var prop in obj) {
          if (obj.hasOwnProperty(prop)) {
            walkTypeProperties(fields.slice(1), obj[prop]);
          }
        }
      }
    }

    var parts = path.split('.');
    walkTypeProperties(parts, sourceObj);
  };

  // Populate member variables with the right data based on routeParams and cmdletDocumentation.
  // Call this after this.cmdletDocumentation has been set.
  this._loadContent = function() {
    if (!this.cmdletDocumentation) return;
    // Attach the cmdlet documentation to the $rootScope so we can access it from filters. This
    // enables us to create hyperlinks whenever we see reference to cmdlets.
    $rootScope.cmdletDocumentation = this.cmdletDocumentation;

    // Hierarchy of resources from Home > Product > Resource > Cmdlet.
    this.contentHeader =
        this.currentCmdlet || this.currentResource ||
        this.currentProduct || defaultContentHeader;

    this.productInfo = null;
    this.resourceInfo = null;
    this.cmdletInfo = null;

    function findElementWithName(name) {
      return function(elem) {
        return (elem.name == name ? elem : null);
      };
    }

    this.productInfo = this.cmdletDocumentation.products.find(findElementWithName(this.currentProduct));
    if (!this.productInfo) return;
    // Use "Google Cloud Storage" while the currentProduct is still "google-cloud-storage".
    if (this.contentHeader == this.currentProduct) {
      this.contentHeader = this.productInfo.longName;
    }

    this.resourceInfo = this.productInfo.resources.find(findElementWithName(this.currentResource));
    if (!this.resourceInfo) return;
    
    this.cmdletInfo = this.resourceInfo.cmdlets.find(findElementWithName(this.currentCmdlet));
  };

  // After we load the cmdlet doc, we cache it in $rootScope. So even through controller reloads we
  // don't need to reissue the HTTP request, parse the JSON, update the objects, etc.
  if ($rootScope.cmdletDocumentation) {
    this.cmdletDocumentation = $rootScope.cmdletDocumentation;
    this._loadContent();
  } else {
    var promise = $http.get('/google-cloud-powershell/data/cmdletsFull.json', { cache: true });
    promise.then(
        function(res) {
          this.cmdletDocumentation = res.data;
          // Products have a name "Google Cloud Storage" and shortName "google-cloud-storage".
          // To simplify our own usage, use shortName as name, and name as longName.
          for (var i = 0; i < this.cmdletDocumentation.products.length; i++) {
            var product = this.cmdletDocumentation.products[i];
            product['longName'] = product.name;
            product['name'] = product.shortName;
          }

          // PowerShell is dropping single-element arrays, which causes problems in controllers.
          // We we explicitly force the following fields to be arrays or null if empty.
          var pathsToUpdate = [
              'products.resources.cmdlets.syntax',
              'products.resources.cmdlets.syntax.parameter',
              'products.resources.cmdlets.parameters',
              'products.resources.cmdlets.parameters.description',
              'products.resources.cmdlets.links',
              'products.resources.cmdlets.examples'
          ];
          for (var i = 0; i < pathsToUpdate.length; i++) {
            this._makeArray(pathsToUpdate[i], this.cmdletDocumentation);
          }

          this._loadContent();
        }.bind(this),
        function(errRes) {
          this.cmdletDocumentation = null;
          // Ensure class members are nulled out.
          this._loadContent();

          this.contentHeader = 'Error';
          this.errorMessage = 'There was an error loading the cmdlet documentation.';
        }.bind(this));
  };
});
