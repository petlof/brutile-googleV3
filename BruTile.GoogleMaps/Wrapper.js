/**
* Brutile GoogleV3
*
* Copyright 2012 Peter Löfås
* 
* This library is free software; you can redistribute it and/or
* modify it under the terms of the GNU Lesser General Public
* License as published by the Free Software Foundation; either 
* version 2.1 of the License, or (at your option) any later version.
* 
* This library is distributed in the hope that it will be useful,
* but WITHOUT ANY WARRANTY; without even the implied warranty of
* MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU
* Lesser General Public License for more details.
* 
* You should have received a copy of the GNU Lesser General Public 
* License along with this library.  If not, see <http://www.gnu.org/licenses/>.
* 
**/

var map;
var baseLayer;
function init() {
    checkIEVersion();
    var mapOptions = {
        units: 'm',
        controls: [],
        maxResolution: 156543.0339,
        theme: null
    };
    var initExt = new OpenLayers.Bounds(253351.88636639, 7494072.399048, 3168965.8928703, 10497741.862124);
    map = new OpenLayers.Map('map', mapOptions);

    var gl = new OpenLayers.Layer.Google("Google", { type: eval(baseLayer), format: "PNG24", numZoomLevels: 20 }, { buffer: 1, transitionEffect: null });
    map.addLayer(gl);
    map.zoomToExtent(initExt);
    gl.mapObject.setTilt(0);

    map.events.register('movestart', map, function () {
        idle = false; tilesloaded = false;
    });

    google.maps.event.addListener(gl.mapObject, "idle", function () {
        // wait for tiles to fade in completely
        setTimeout(function () {
            idle = true;
        },
                0);
    });
    google.maps.event.addListener(gl.mapObject, "tilesloaded", function () {
        // wait for tiles to fade in completely
        setTimeout(function () {
            tilesloaded = true;
        },
                150);
    });
    loaded = true;
}

idle = false;
tilesloaded = false;
loaded = false;

function isZoomDone() {    
    return tilesloaded && idle;
}


function isIdle() {
    return idle;
}

function isTilesLoaded() {
    return tilesloaded;
}

function getHtml() {
    return "<html><head>" + document.getElementsByTagName("head")[0].innerHTML + "</head><body onload=\"init()\">" + document.body.innerHTML + "</body></html>";
}

function isLoaded() {
    return loaded;
}

function matrixToArray(matrix) {
    return matrix.substr(7, matrix.length - 8).split(', ');
}

function getExtent() {
    return map.getExtent().toBBOX();
}

function setExtent(xmin, ymin, xmax, ymax, level) {
    //  zoomDone = 0;
    //map.setCenter(new OpenLayers.LonLat((xmin+xmax)/2, (ymin+ymax)/2),level,true,false);   
    var uLbefore = map.getLonLatFromPixel(new OpenLayers.Pixel(0, 0));
    var zbefore = map.getZoom();

    map.zoomToExtent(new OpenLayers.Bounds(xmin, ymin, xmax, ymax), true); //, level, true, false);   

    var zafter = map.getZoom();

    var ulAfter = map.getPixelFromLonLat(uLbefore);

    if (zafter == zbefore && Math.abs(ulAfter.x) < 256 && Math.abs(ulAfter.y) < 256) {
        tilesloaded = true;
    }
}

function getTileURLs() {

    var els = document.getElementById('map').getElementsByTagName('img');
    var images = {};
    var mapNode = document.getElementById('map');
    var idx = 0;
    var matrixOffset = null;
    for (i = 0; i < els.length; i++) {
        var n = els[i].parentNode.parentNode;
        if (matrixOffset == null) {
            var offsetX = 0;
            var offsetY = 0;
            do {
                offsetX += n.offsetLeft;
                offsetY += n.offsetTop;
                if (n.style.webkitMatrix != null) {
                    var matrix = matrixToArray(n.style.webkitMatrix);
                    offsetX += parseInt(matrix[4]);
                    offsetY += parseInt(matrix[5]);
                }
                n = n.parentNode;
            } while (n != null && n != mapNode);

            matrixOffset = { x: offsetX, y: offsetY };
        }
        var oX = els[i].parentNode.offsetLeft + matrixOffset.x;
        var oY = els[i].parentNode.offsetTop + matrixOffset.y;
        var z = 0;
        var children = els[i].parentNode.childNodes;
        for (n = 0; n < children.length; n++) {
            if (children[i] == els[i]) {
                z = n;
                break;
            }
        }
        images["item_" + idx] = {
            url: els[i].src,
            left: oX,
            top: oY,
            index: i,
            zIndex: z
        };
        idx++;
    }
    images.length = idx;

    return images;
}

function getResolutions() {
    var ret = "";
    for (var i = 0; i < map.layers[0].resolutions.length; i++) {
        if (ret != "") {
            ret += ",";
        }
        ret += map.layers[0].resolutions[i];
    }
    return ret;
}

function getOLCenter() {
    return map.getCenter().toString();
}

function getOLExtent() {
    return map.getExtent().toBBOX();
}



function updateSize(w, h) {
    idle = false;
    tilesloaded = false;
    var c = map.getCenter();
    var z = map.getZoom();
    document.getElementById("map").style.width = w + "px";
    document.getElementById("map").style.height = h + "px";
    
    map.updateSize();
    map.setCenter(c, z + 1, true, false);
    map.setCenter(c, z, true, false);
    return map.getSize().w + "," + map.getSize().h;
}

function getInternetExplorerVersion()
    // Returns the version of Windows Internet Explorer or a -1
    // (indicating the use of another browser).
{
    var rv = -1; // Return value assumes failure.
    if (navigator.appName == 'Microsoft Internet Explorer') {
        var ua = navigator.userAgent;
        var re = new RegExp("MSIE ([0-9]{1,}[\.0-9]{0,})");
        if (re.exec(ua) != null)
            rv = parseFloat(RegExp.$1);
    }
    return rv;
}
function checkIEVersion() {
    var msg = "You're not using Windows Internet Explorer.";
    var ver = getInternetExplorerVersion();
    if (ver > -1) {
        if (ver >= 11.0)
            msg = "You're using Windows Internet Explorer 11.";
        else if (ver == 10.0)
                msg = "You're using Windows Internet Explorer 10.";
        else if (ver == 9.0)
            msg = "You're using Windows Internet Explorer 9.";
        if (ver >= 8.0)
            msg = "You're using Windows Internet Explorer 8.";
        else if (ver == 7.0)
            msg = "You're using Windows Internet Explorer 7.";
        else if (ver == 6.0)
            msg = "You're using Windows Internet Explorer 6.";
        else
            msg = "You should upgrade your copy of Windows Internet Explorer";
    }
}

