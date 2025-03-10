document.addEventListener("DOMContentLoaded", function () {

    var extLinks = document.querySelectorAll('a[href*="://"]');
    for (var i = 0; i < extLinks.length; i++) {
        if (!extLinks[i].href.startsWith(window.location.origin)) {
            extLinks[i].setAttribute('target', '_blank');
        }
    }

    loadModalScript();
    console.log("Script Loaded");

    document.querySelectorAll('#paywall-overlay').forEach((overlay, index) => {
        const container = overlay.closest('.paywall-section');
        if (!container) return;

        const paywallConfig = container.querySelector('#paywall-config');
        if (!paywallConfig || !paywallConfig.dataset.price) return;

        const price = paywallConfig.getAttribute("data-price");
        const uniqueId = generateUniqueId(index);

        if (localStorage.getItem('paywall_unlocked_' + uniqueId) === 'true') {
            console.log(`Content ${uniqueId} is already unlocked`);
            unlockContent(uniqueId);
        }

        overlay.addEventListener("click", function (event) {
            if (event.target.id === "payButton") {
                event.preventDefault();
                event.target.disabled = true;
                event.target.textContent = "Loading...";
                handleBitcoinPayment(price, event.target, uniqueId);
            }
        });
    });
});


function generateUniqueId(index) {
    const urlPath = window.location.pathname;
    return btoa(urlPath + "_paywall_" + index);
}

function unlockContent(uniqueId) {
    document.querySelectorAll('#paywall-overlay').forEach((overlay, index) => {
        const generatedId = generateUniqueId(index);
        if (generatedId === uniqueId) {
            overlay.style.display = 'none';
            overlay.previousElementSibling.style.display = 'block';
        }
    });
    localStorage.setItem('paywall_unlocked_' + uniqueId, 'true');
}

function handleBitcoinPayment(amount, button, uniqueId) {
    const url = BTCPAYSERVER_URL + "/plugins/" + BTCPAYSERVER_STORE_ID + "/ghost/api/paywall/create-invoice?amount=" + amount;
    fetch(url, {
        method: "GET",
        headers: { "Content-Type": "application/json" }
    })
        .then(response => response.json())
        .then(data => {
            console.log("Invoice created:", data);
            if (data.id) {
                showBTCPayModal(data, uniqueId);
            }
            else {
                button.disabled = false;
                button.textContent = "Unlock with Bitcoin";
                alert("Error: Failed to generate invoice. Please contact admin");
            }
        })
        .catch(error => {
            button.disabled = false;
            button.textContent = "Unlock with Bitcoin";
            console.error("Payment initiation failed. Error:", error);
            alert("Error: Payment initiation failed. Please try again.");
        });
}



const showBTCPayModal = function (data, uniqueId) {
    console.log('Triggered showBTCPayModal()');

    if (data.id == undefined) {
        console.error('No invoice id provided, aborting.');
    }
    window.btcpay.setApiUrlPrefix(BTCPAYSERVER_URL);
    window.btcpay.showInvoice(data.id);

    let invoice_paid = false;
    window.btcpay.onModalReceiveMessage(function (event) {
        if (isObject(event.data)) {
            if (event.data.status) {
                switch (event.data.status.toLowerCase()) {
                    case 'complete':
                    case 'paid':
                    case 'confirmed':
                    case 'processing':
                    case 'settled':
                        invoice_paid = true;
                        console.log('Invoice paid.');
                        unlockContent(uniqueId);
                        break;
                    case 'expired':
                        window.btcpay.hideFrame();
                        console.error('Invoice expired.');
                        break;
                    case 'invalid':
                        window.btcpay.hideFrame();
                        console.error('Invalid invoice.');
                        break;
                    default:
                        console.error('Unknown status: ' + event.data.status);
                }
            }
        } else {
            if (event.data === 'close') {
                if (invoice_paid === true) {
                    unlockContent(uniqueId);
                    console.log('Invoice paid.');
                }
                console.log('Modal closed.')
            }
        }
    });
    const isObject = obj => {
        return Object.prototype.toString.call(obj) === '[object Object]'
    }
};


const loadModalScript = () => {
    const script = document.createElement('script');
    script.src = BTCPAYSERVER_URL + '/plugins/' + BTCPAYSERVER_STORE_ID + '/ghost/api/btcpay-ghost.js';
    document.head.appendChild(script);
    script.onload = function () {
        console.log('External modal script loaded successfully.');
    };
    script.onerror = function () {
        console.error('Error loading the external modal script.');
    };
}