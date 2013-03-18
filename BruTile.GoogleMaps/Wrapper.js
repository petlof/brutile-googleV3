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
    var map_options = {
        units: 'm',
        controls: [],
        maxResolution: 156543.0339,
        theme: null
    };
    var initExt = new OpenLayers.Bounds(253351.88636639, 7494072.399048, 3168965.8928703, 10497741.862124);
    map = new OpenLayers.Map('map', map_options);

    gl = new OpenLayers.Layer.Google("Google", { type: eval(baseLayer), format: "PNG24", numZoomLevels: 20 }, { buffer: 1, transitionEffect: null });
    map.addLayer(gl);
    map.zoomToExtent(initExt);

    /*onDomChange(function () {
    idle = false; tilesloaded = false;
    });*/
    gl.mapObject.setTilt(0);

    //google.maps.event.addListener(gl.mapObject, "bounds_changed", function () { idle = false; tilesloaded = false; /*alert("bounds_changed");*/ });

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
    var ULbefore = map.getLonLatFromPixel(new OpenLayers.Pixel(0, 0));
    var zbefore = map.getZoom();

    map.zoomToExtent(new OpenLayers.Bounds(xmin, ymin, xmax, ymax), true); //, level, true, false);   

    var zafter = map.getZoom();

    var ulAfter = map.getPixelFromLonLat(ULbefore);

    if (zafter == zbefore && Math.abs(ulAfter.x) < 256 && Math.abs(ulAfter.y) < 256) {
        tilesloaded = true;
    }
}

function getTileURLs() {

    var els = document.getElementById('map').getElementsByTagName('img');
    var images = {};
    var mapNode = document.getElementById('map')
    idx = 0;
    matrixOffset = null;
    //alert(els.length);
    for (i = 0; i < els.length; i++) {
        //if (els[i].id == "" && els[i].style.width == "256px" && els[i].style.height == "256px") {

            var n = els[i].parentNode.parentNode;
            if (matrixOffset == null) {
                offsetX = 0;
                offsetY = 0;
                do {
                    offsetX += n.offsetLeft;
                    offsetY += n.offsetTop;
                    if (n.style.webkitMatrix != null) {
                        matrix = matrixToArray(n.style.webkitMatrix);
                        offsetX += parseInt(matrix[4]);
                        offsetY += parseInt(matrix[5]);
                    }
                    n = n.parentNode;
                } while (n != null && n != mapNode);

                matrixOffset = { x: offsetX, y: offsetY };
            }
            oX = els[i].parentNode.offsetLeft + matrixOffset.x;
            oY = els[i].parentNode.offsetTop + matrixOffset.y;
            z = 0;
            children = els[i].parentNode.childNodes;
            for (n = 0; n < children.length; n++)
            {
                if (children[i] == els[i]) {
                    z = n;
                    break;
                }
            }
            images["item_" + idx] = { url: els[i].src,
                left: oX,
                top: oY,
                index: i,
                zIndex : z
            };
            idx++;
        //}
    }
    images.length = idx;

    return images;
}

function getResolutions() {
    ret = "";
    for (i = 0; i < map.layers[0].resolutions.length; i++) {
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



