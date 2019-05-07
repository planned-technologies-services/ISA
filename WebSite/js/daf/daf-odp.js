/*!
* Data Aquarium Framework - Offline Data Processor
* Copyright 2008-2017 Code On Time LLC; Licensed MIT; http://codeontime.com/license
*/

(function () {
    var _app = $app,
        _odp,
        _controllers = {};

    _odp = _app.odp = {
        enabled: function (value) {
            if (arguments.length)
                this._enabled = value;
            if (!this._checkedSettings && typeof __settings !== 'undefined') {
                this._checkedSettings = true;
                var odpSettings = __settings && __settings.odp;
                if (odpSettings) {
                    if (odpSettings.enabled == false)
                        this._enabled = false;
                    if (odpSettings.pageSize)
                        _odp._pageSize = odpSettings.pageSize;
                }
            }
            return _app.touch && this._enabled != false;
        },
        offline: function () { return false; },
        start: function (enabled) {
            _odp.enabled(enabled);
            _odp.controllers = {};
            _odp.functions = {};

        },
        invoke: function (dataView, params) {
            var that = this,
                servicePath = dataView.get_servicePath(),
                methodName = params.url.substring(servicePath.length + 1),
                deferred = $.Deferred(),
                odp,
                runOnServer = true;

            if (_app.odp.enabled()) {
                odp = dataView.odp;
                // assign an Offline Data Processor instance to all members of modal hierarchy
                if (!odp && methodName == 'GetPage') {
                    var enabled = dataView.tagged(/\odp\-enabled\-(\w+)\b/)
                    enabled = enabled ? enabled[1] : 'auto';
                    if (enabled != 'none') {
                        if (dataView._dataViewFieldParentId) {
                            var master = _app.find(dataView._filterSource);
                            if (master)
                                odp = master.odp;
                        }
                        else {
                            var parent = dataView.get_parentDataView();
                            if (parent) {
                                if (dataView._filterSource)
                                    odp = parent.odp;
                                if (!odp)
                                    if (parent.odp)
                                        odp = parent.odp;
                                    else {
                                        odp = new _app.OfflineDataProcessor(dataView);
                                        odp.enabled = enabled;
                                    }
                            }
                        }
                        if (!odp && dataView._isModal) {
                            var currentDataView = $app.touch.dataView();
                            if (currentDataView)
                                odp = currentDataView.odp;
                        }
                    }
                    dataView.odp = odp;
                }
                // execute request locally when needed and skip execution on the server
                if (odp && odp.invoke({ method: methodName, dataView: dataView, params: params, deferred: deferred }))
                    runOnServer = false;
            }
            if (runOnServer)
                $.ajax(params).done(function (result) {
                    deferred.resolve(result);
                }).fail(function (jqXHR, textStatus, error) {
                    deferred.reject(jqXHR, textStatus, error);
                });
            return deferred.promise();
        },
        func: function (text) {
            var result = _odp.functions[text];
            if (!result)
                result = _odp.functions[text] = eval(text);
            return result;
        }
    }

    //
    // Implementation of OfflineDataProcessor for local data processing
    //

    _app.OfflineDataProcessor = function (dataView) {
        var that = this;
        that._dataView = dataView;
        that._state = 'inactive';
        that._data = {};
        that._dataLoadMap = {};    // map of loaded data objects
        that._dataLoadedKeys = {}; // map of loaded object keys
        that._log = [];
        that._tracking = {};
    }

    _app.OfflineDataProcessor.prototype = {
        is: function (value) {
            var that = this;
            if (arguments.length) {
                if (value.match(/^:/)) {
                    if (value == ':dirty')
                        return that._log.length > 0;
                    return (that._state) == value.substring(1);
                }
                else
                    that._state = value;
            }
            else
                return that._state;

        },
        root: function (value) {
            return arguments.length == 1 ? this._dataView == value : this._dataView;
        },
        invoke: function (data) {
            var that = this,
                result,
                dataView = data.dataView,
                controller = dataView._controller,
                lastArgs = dataView._lastArgs,
                isRoot = this._dataView == dataView,
                method = data.method;
            if (isRoot) {
                if (method == 'GetPage' && dataView.get_lastCommandName() == 'New')
                    that.is('active');
                else if (method == 'Execute' && lastArgs && lastArgs.CommandName.match(/^(Insert|Update)$/)) {
                    // commit the log 
                    that._log.splice(0, 0, data.params.data);
                    // call "CommitServerRequestHandler" passing "primaryKeys form ODP and the log
                    that.is('inactive');
                }
            }
            else if (method == 'Execute' && lastArgs && lastArgs.CommandName.match(/^(Insert|Update|Delete)$/)) {
                that.is('active');
                // create an empty array in odp.data.objects.Products= []
                that._log.push(data.params.data);
                that._tracking[dataView._controller] = true;
            }
            if (method.match(/^(GetPage|Execute|GetListOfValues)$/) && that.tracking(dataView)) {
                $.when(that.getControllers([controller])).done(function () {
                    $.when(that.getData(controller, dataView.get_externalFilter())).done(function () {
                        that.execute(data);
                    });
                });
                result = true;
            }
            return !!result;
        },
        tracking: function (dataView) {
            return !!this._tracking[dataView._controller];
        },
        getControllers: function (controllers) {
            var missing = [],
                deferred = $.Deferred(),
                cachedControllers = _odp.controllers;
            controllers.forEach(function (controller) {
                if (!cachedControllers[controller])
                    missing.push(controller);
            })
            if (missing.length)
                $.ajax({
                    url: __servicePath + '/getcontrollerlist',
                    method: 'POST',
                    cache: false,
                    dataType: 'text',
                    data: JSON.stringify({ controllers: missing })
                }).done(function (result) {
                    var controllers = JSON.parse(JSON.parse(result).d);
                    controllers.forEach(function (obj) {
                        var dataController = obj.dataController;
                        cachedControllers[dataController.name] = dataController;
                        // create maps
                        var map = dataController._map = { key: {}, fields: {} };
                        var key = dataController.key = [];
                        dataController.fields.forEach(function (f) {
                            map.fields[f.name] = f;
                            if (f.isPrimaryKey) {
                                key.push(f);
                                map.key[f.name] = f;
                            }
                        });
                    });
                    deferred.resolve();
                });
            else
                deferred.resolve();
            return deferred.promise();
        },
        getData: function (controller, externalFilter) {
            var that = this,
                deferred = $.Deferred(),
                rowCount,
                loadMapKey,
                pageIndex = 0;

            function loadPage(pageIndex) {
                var pageSize = _odp._pageSize || 100;
                _app.execute({
                    controller: controller,
                    view: 'offline',
                    pageSize: pageSize,
                    pageIndex: pageIndex,
                    requiresRowCount: pageIndex == 0,
                    externalFilter: externalFilter
                }).done(function (result) {
                    if (pageIndex == 0)
                        rowCount = result.totalRowCount;
                    that.addData(controller, result[controller]);
                    rowCount -= pageSize;
                    if (rowCount < 0)
                        deferred.resolve();
                    else
                        loadPage(pageIndex + 1);
                });
            }

            if (!that._dataLoadMap[controller])
                that._dataLoadMap[controller] = {}
            loadMapKey = externalFilter ? JSON.stringify(externalFilter) : '_all';
            if (!that._dataLoadMap[controller][loadMapKey]) {
                that._dataLoadMap[controller][loadMapKey] = true;
                that._dataLoadedKeys[controller] = {};
                loadPage(0);
            }
            else
                deferred.resolve();

            return deferred.promise();
        },
        addData: function (controller, data) {
            var that = this,
                c = _odp.controllers[controller],
                pk = c.key,
                loadedKey = that._dataLoadedKeys[controller],
                list = that._data[controller];
            if (!list)
                list = that._data[controller] = [];
            data.forEach(function (obj) {
                var key = [];
                pk.forEach(function (k) {
                    key.push(obj[k.name]);
                });
                key = key.join(',');
                if (!loadedKey[key]) {
                    loadedKey[key] = true;
                    list.push(obj);
                }
            });
        },
        execute: function (options) {
            var that = this,
                method = options.method,
                methodArgs = JSON.parse(htmlDecode(options.params.data)),
                args = methodArgs.args,
                request = methodArgs.request,
                values = args ? args.Values : null,
                controller = _odp.controllers[methodArgs.controller],
                commandName = args ? args.CommandName : null,
                map = controller._map,
                filter = [], params = {}, paramCount = 0,
                objects;

            function resolve(result) {
                options.deferred.resolve(JSON.stringify({ d: result }));
            }

            if (method == 'Execute') {
                var actionResult = {
                    "Tag": null,
                    "Errors": [],
                    "Values": [
                        //{
                        //    "Name": "ProductID",
                        //    "OldValue": null,
                        //    "NewValue": 201,
                        //    "Modified": true,
                        //    "ReadOnly": false,
                        //    "Value": 201,
                        //    "Error": null
                        //}
                    ],
                    "Canceled": false,
                    "NavigateUrl": null,
                    "ClientScript": null,
                    "RowsAffected": 0,
                    "Filter": null,
                    "SortExpression": null,
                    "RowNotFound": false
                };
                // translate values into filter expression
                if (commandName == 'Update' || commandName == 'Delete') {
                    values.forEach(function (fv) {
                        if (map.key[fv.Name]) {
                            if (filter.length)
                                filter.push('&&');
                            filter.push('(this.' + fv.Name + '==params.p' + paramCount + ')');
                            params['p' + paramCount++] = fv.OldValue;
                        }
                    });
                    that.find({ controller: controller.name, filter: filter, params: params, limit: 1, delete: commandName == 'Delete' }).done(function (result) {
                        // process values and update the item for "Update" command
                        var target = result[0];
                        if (target && commandName == 'Update')
                            values.forEach(function (fv) {
                                if (fv.Modified)
                                    target[fv.Name] = fv.NewValue;
                            });
                        actionResult.RowsAffected = result.length;
                        actionResult.RowNotFound = result.length == 0;
                        resolve(actionResult);
                    });
                }
                else if (commandName == 'Insert') {
                    // insert a new row but check for duplicates
                }
            }
            else if (method == 'GetPage') {
                // handle request here.
                var viewPage = {};
                (request.Filter || []).forEach(function (fe) {
                });
                resolve(viewPage);
            }
            else
                _app.alert('Unknown method: ' + method);

        },
        find: function (options) {
            var that = this,
                deferred = $.Deferred(),
                controller = options.controller,
                filter = options.filter, params = options.params,
                sort = options.sort,
                objects = that._data[controller], result = [], toDelete = [],
                filterFunc;
            // filter
            if (filter) {
                filterFunc = _odp.func('(function(params){return ' + filter + '})');
                for (var i = 0; i, objects.length; i++) {
                    var obj = objects[i];
                    if (filterFunc.call(obj, params)) {
                        result.push(obj);
                        if (options.delete)
                            toDelete.push(i);
                        if (options.limit == result.length)
                            break;
                    }
                }
                for (i = toDelete.length - 1; i >= 0; i--)
                    objects.splice(toDelete[i], 1);
            }
            else
                result = objects.slice(0);
            // sort
            if (sort)
                result.sort(_odp.func(that._sortFunc(sort)));
            // complete
            deferred.resolve(result);
            return deferred.promise();
        },
        _sortFunc: function (sort) {
            var func = ['(function(a,b){'],
                iterator = /(\w+)(\s+(asc|desc))?/ig,
                m, first = true;
            func.push('var result=0;');
            while (m = iterator.exec(sort)) {
                if (first)
                    first = false;
                else
                    func.push('if (result != 0) return result;');
                func.push(String.format('if (a.{0} < b.{0}) result = -1; else if(a.{0} > b.{0}) result = 1;', m[1]));
                if (m[3] && m[3].match(/^desc/i))
                    func.push('if (result != 0) result *=-1;');
            }
            func.push('return result;');
            func.push('})');
            return func.join('\n');
        },
        _filterFunc: function (filter) {
            var func = ['(function(a,b){'];
            func.push('})');
            func = func.join('\n');
            return func;
        }
    };

    function htmlDecode(s) {
        var decoder = _app._htmlDecoder;
        if (!decoder)
            decoder = _app._htmlDecoder = document.createElement("textarea");;
        decoder.innerHTML = s;
        return decoder.value;

    }

    _app.odp.start(false);

})();