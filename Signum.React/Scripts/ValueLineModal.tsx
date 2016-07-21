﻿import * as React from 'react'
import { Modal, ModalProps, ModalClass, ButtonToolbar } from 'react-bootstrap'
import { openModal, IModalProps } from './Modals';
import { SelectorMessage, JavascriptMessage } from './Signum.Entities'
import { TypeInfo, TypeReference, Binding } from './Reflection'
import { FormGroupStyle, TypeContext } from './TypeContext'
import { ValueLineType, ValueLine} from './Lines/ValueLine'


interface ValueLinePopupModal extends React.Props<ValueLineModal>, IModalProps {

    options: ValueLinePopupOptions;
}

export default class ValueLineModal extends React.Component<ValueLinePopupModal, { show: boolean; value?: any }>  {

    constructor(props: ValueLinePopupModal) {
        super(props);

        this.state = { show: true, value: this.props.options.value };
    }

    selectedValue: any;
    hanleOkClick = () => {
        this.selectedValue = this.state.value;
        this.setState({ show: false });

    }

    handleCancelClicked = () => {
        this.setState({ show: false });
    }

    handleOnExited = () => {
        this.props.onExited!(this.selectedValue);
    }

    render() {
        const o = this.props.options;

        const ctx = new TypeContext(undefined, undefined, undefined as any, Binding.create(o, ops => ops.value));

        return <Modal bsSize="lg" onHide={this.handleCancelClicked} show={this.state.show} onExited={this.handleOnExited}>
            <Modal.Header closeButton={true}>
                <h4 className="modal-title">
                    {o.title === undefined ? SelectorMessage.ChooseAValue.niceToString() : o.title}
                </h4>
            </Modal.Header>

            <Modal.Body>
                <p>
                    {o.message === undefined ? SelectorMessage.PleaseChooseAValueToContinue.niceToString() : o.message}
                </p>
                <ValueLine
                    ctx={ctx}
                    type={o.type}
                    labelText= { o.labelText  }
                    valueLineType={o.valueLineType}
                    formatText={o.format}
                    unitText={o.unit}
                    formGroupStyle={o.labelText ? "Basic" : "SrOnly"}/>
            </Modal.Body>
            <Modal.Footer>
                <button className ="btn btn-primary sf-entity-button sf-close-button sf-ok-button" onClick={this.hanleOkClick}>
                    {JavascriptMessage.ok.niceToString() }
                </button>
            </Modal.Footer>
        </Modal>;
    }
    
    static show(options: ValueLinePopupOptions): Promise<any> {
        return openModal<any>(<ValueLineModal options={options}/>);
    }
}

export interface ValueLinePopupOptions {
    type: TypeReference
    valueLineType?: ValueLineType;
    value?: any
    title?: string;
    message?: string;
    labelText?: string;
    format?: string;
    unit?: string;
}



