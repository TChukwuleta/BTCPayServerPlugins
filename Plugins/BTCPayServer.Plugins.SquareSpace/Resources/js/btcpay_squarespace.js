(function () {
    console.log("[BTCPay][Init] Script loaded");

    if (window.__btcpay_injected) return;
    window.__btcpay_injected = true;

    function getCartData() {
        const cartScript = document.querySelector('#sqs-cart-root > script[type="application/json"]');
        if (!cartScript) return null;

        let cartData;
        try {
            //return JSON.parse(cartScript.innerText).cart;
            cartData = JSON.parse(cartScript.innerText).cart;
            console.log("[BTCPay][Cart] Cart loaded", cartData);
            return cartData;
        } catch (err) {
            console.error("[BTCPay][Cart] JSON parse error", err);
            return null;
        }
    }

    function injectButton() {
        const checkoutContainer = document.querySelector('.cart-checkout');
        if (!checkoutContainer) return;

        if (document.querySelector("#btcpay-bitcoin-btn")) return; 

        const btcBtn = document.createElement("button");
        btcBtn.type = "button";
        btcBtn.id = "btcpay-bitcoin-btn";

        const checkoutBtn = checkoutContainer.querySelector('a, button');
        if (checkoutBtn) {
            const styles = window.getComputedStyle(checkoutBtn);
            btcBtn.style.background = "#51B13E";
            btcBtn.style.color = "#FFFFFF";
            btcBtn.style.fontSize = styles.fontSize;
            btcBtn.style.fontWeight = styles.fontWeight;
            btcBtn.style.fontFamily = styles.fontFamily;
            btcBtn.style.padding = styles.padding;
            btcBtn.style.borderRadius = styles.borderRadius;
            btcBtn.style.border = styles.border;
            btcBtn.style.width = "100%";
            btcBtn.style.cursor = "pointer";
            btcBtn.style.textAlign = "center";
            btcBtn.style.marginBottom = "12px";
        }
        btcBtn.innerText = "Pay with BTCPay Server";

        checkoutContainer.prepend(btcBtn);

        btcBtn.addEventListener("click", async (e) => {
            e.preventDefault();
            btcBtn.innerText = "Loading...";
            btcBtn.disabled = true;

            const cartData = getCartData();
            if (!cartData || !cartData.items || cartData.items.length === 0) {
                alert("Unable to read cart data");
                btcBtn.innerText = "Pay with BTCPay Server";
                btcBtn.disabled = false;
                return;
            }

            const payload = {
                cartToken: cartData.cartToken,
                cartData: JSON.stringify(cartData),
                cartId: cartData.id,
                customerEmail: cartData.shopperEmail || "",
                currency: cartData.subtotal.currencyCode,
                amount: cartData.grandTotal.decimalValue,
                items: cartData.items.map(item => ({
                    title: item.productName,
                    quantity: item.quantity,
                    unitPrice: item.unitPrice.decimalValue
                }))
            };

            console.log("[BTCPay][Payload]", payload);

            try {
                const url = BTCPAYSERVER_URL + "/plugins/" + BTCPAYSERVER_STORE_ID + "/squarespace/public/cart";
                const res = await fetch(url, {
                    method: "POST",
                    headers: { "Content-Type": "application/json" },
                    body: JSON.stringify(payload)
                });

                if (!res.ok) {
                    throw new Error("Server returned " + res.status);
                }
                const data = await res.json();
                console.log(data);
                if (data.paymentUrl) {
                    window.location.href = data.paymentUrl;
                } else {
                    throw new Error("Missing paymentUrl");
                }

            } catch (err) {
                console.error("[BTCPay][Error]", err);
                alert("Failed to create Bitcoin invoice.");
                btcBtn.innerText = "Pay with BTCPay Server";
                btcBtn.disabled = false;
            }
        });
        console.log("[BTCPay][UI] BTCPay button injected");
    }

    function waitForBody(callback) {
        if (document.body) {
            callback();
        } else {
            document.addEventListener("DOMContentLoaded", () => callback());
        }
    }

    waitForBody(() => {
        const observer = new MutationObserver(() => injectButton());
        observer.observe(document.body, { childList: true, subtree: true });

        injectButton();
    });
})();
